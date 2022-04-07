using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Azure.Storage.Blobs;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace BillingRequest
{
    public static class BillingRequest
    {
        /// <summary>
        /// Used to capture billing information on a line by line basis 
        /// </summary>
        public class ResourceBillingElement
        {
            public double PreTaxCost { get; }
            public string ResourceType { get; }
            public string Currency { get; }
            public ResourceBillingElement(double preTaxCost, string resourceType, string currency)
            {
                PreTaxCost = preTaxCost;
                ResourceType = resourceType;
                Currency = currency;
            }
        }
        
        /// <summary>
        /// Creates an in memory representation of a excel file you retrieve from blob storage.
        /// </summary>
        /// <param name="connectionString"></param>
        /// <param name="containerName"></param>
        /// <returns></returns>
        private static MemoryStream ReadTemplateFromBlob(string connectionString, string containerName)
        {
            // Get xlsx template from specified container
            BlobContainerClient blobContainerClient = new BlobContainerClient(connectionString, containerName);
            var blob = blobContainerClient.GetBlobClient("Template.xlsx");

            MemoryStream memoryStream = new MemoryStream();

            // Download blob as a stream to be manipulated
            blob.DownloadTo(memoryStream);

            return memoryStream;
        }
        
        /// <summary>
        /// Creates a zero index on the collection.
        /// Creates a similar work flow as Array.mapi in F#
        /// </summary>
        /// <param name="self"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        private static IEnumerable<(T item, int index)> WithIndex<T>(this IEnumerable<T> self)       
            => self.Select((item, index) => (item, index));
        
        /// <summary>
        /// Populates an excel stream using bespoke logic
        /// </summary>
        /// <param name="columns">Column names to place at the top before writing out billing information</param>
        /// <param name="costElements">Array/Enumerable of elements to be written out</param>
        /// <param name="sheetName"></param>
        /// <param name="stream">The stream of the template xlsx file we extracted from blob storage</param>
        /// <returns></returns>
        private static MemoryStream PopulateExcelWorksheet (IEnumerable<string> columns, IEnumerable<ResourceBillingElement> costElements, string sheetName, MemoryStream stream)
        {
            // Link to existing workbook
            using var wbook = new XLWorkbook(stream);

            // Link to sheet
            var ws = wbook.Worksheet(sheetName);

            // Set row offset
            const int startRow = 2;
            
            //set column names
            foreach (var (columnName, index) in columns.WithIndex())
            {
                ws.Cell(startRow, index + 1).Value = columnName;
            }
            
            //input the cost values relative to the start values - columns are 1 indexed
            foreach (var (costElement, index) in costElements.WithIndex())
            {
                ws.Cell(startRow + 1 + index, 1).Value = costElement.PreTaxCost;
                ws.Cell(startRow + 1 + index, 2).Value = costElement.ResourceType;
                ws.Cell(startRow + 1 + index, 3).Value = costElement.Currency;
            }

            // Save file
            wbook.SaveAs(stream);

            // Create new stream
            MemoryStream memoryStream = stream;

            // Return stream
            return memoryStream;
        }
        
        /// <summary>
        /// Gets an authorisation token based using a managed identity
        /// </summary>
        /// <param name="log"></param>
        /// <returns></returns>
        private static async Task<string> GetAuthorizationToken(ILogger log)
        {
            
            // For using Service Principal approach - see azure details class
            
            // ClientCredential cc = new ClientCredential(AzureDetails.ClientID, AzureDetails.ClientSecret);
            // var context = new AuthenticationContext("https://login.microsoftonline.com/" + AzureDetails.TenantID);
            // var result = context.AcquireTokenAsync("https://management.azure.com/", cc);
            // if (result == null)
            // {
            //     throw new InvalidOperationException("Failed to obtain the Access token");
            // }
            
            //set interactiveCredentials = true for local testing
            var credential = new DefaultAzureCredential(includeInteractiveCredentials: true);
            string[] scopes = { "https://management.azure.com/.default" };
            var acquireToken= await credential.GetTokenAsync(new TokenRequestContext(scopes));

            return acquireToken.Token;
        }
        
        
        /// <summary>
        /// Performs an HTTP request based on specified details
        /// </summary>
        /// <param name="getRequest"></param>
        /// <param name="client"></param>
        /// <returns></returns>
        private static async Task<string> MakeRequestAsync(HttpRequestMessage getRequest, HttpClient client)
        {
            var response = await client.SendAsync(getRequest).ConfigureAwait(false);
            var responseString = string.Empty;
            try
            {
                response.EnsureSuccessStatusCode();
                responseString = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            }
            catch (HttpRequestException a)
            {
                
                Console.WriteLine(a.Message);
            }

            return responseString;
        }
        
        /// <summary>
        /// Queries the Azure Management API for billing information related to a particular resource group
        /// </summary>
        /// <param name="token"></param>
        /// <param name="log"></param>
        /// <returns>
        /// A (JSON formatted) string representing the billing information.
        /// An empty string returned typically means that you didn't have the correct permissions to access the resource info.</returns>
        private static async Task<string> RetrieveBillingInformation(string token, ILogger log) 
        {
            var subcriptionID = "<Subscription ID>";
            var resourceGroupName = "<ResourceGroupName>";
            
            //for more info on preparing the request - https://docs.microsoft.com/en-us/rest/api/cost-management/query/usage
            var queryAddress = $"https://management.azure.com/subscriptions/{subcriptionID}/resourceGroups/{resourceGroupName}/providers/Microsoft.CostManagement/query?api-version=2019-11-01";
            
            HttpClient client = new HttpClient();
            client.BaseAddress = new Uri(queryAddress);
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Add("Authorization", "Bearer " + token);

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, client.BaseAddress);

            // JSON file on Azure function can be found at : D:/home/site/wwwroot/BillingInformationQuery.json
            JObject body = JObject.Parse(System.IO.File.ReadAllText(@"D:/home/site/wwwroot/BillingInformationQuery.json"));
            var content = new StringContent(body.ToString(), Encoding.UTF8, "application/json");
            request.Content = content;
            log.LogInformation("Sent request to management");
            return await MakeRequestAsync(request, client);

        }
        
        [FunctionName("WriteCostReportToBlobStorage")]
        public static async Task<IActionResult> RunAsync([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            
            log.LogInformation("Getting token");
            string token = await GetAuthorizationToken(log);
            
            log.LogInformation("Got token");
            var azureApiResponse = await RetrieveBillingInformation(token, log);

            JObject test = JObject.Parse(azureApiResponse);

            //get column headers
            var columns =
                from p in test["properties"]["columns"]
                select (string ) p["name"];
            
            //extract out the cost elements
            var costElements =
                from p in test["properties"]["rows"]
                select new ResourceBillingElement(preTaxCost: (double) p[0], resourceType: (string ) p[1],currency: (string ) p[2]);

            var containerName = "costcontainer";
            
            var client = new SecretClient(new Uri("https://<VAULTNAME>.vault.azure.net/"), new DefaultAzureCredential(includeInteractiveCredentials: true));

            KeyVaultSecret blobConnectionStringSecret = await client.GetSecretAsync("BlobStorageConnectionString");
            
            log.LogInformation("Got Blob Storage Connection String");
            
            var excelTemplate = ReadTemplateFromBlob(blobConnectionStringSecret.Value, containerName);

            MemoryStream filledInValues = PopulateExcelWorksheet(columns, costElements, "Sheet1", excelTemplate);
            
            // initialize BobClient - rename the output blob whatever you need it to be
            BlobClient blobClient = new BlobClient(
                connectionString: blobConnectionStringSecret.Value, 
                blobContainerName: containerName, 
                blobName: "sampleBlobFileTest.xlsx");
                
            // upload the file from the start of the stream
            filledInValues.Position = 0;

            await blobClient.UploadAsync(filledInValues, overwrite: true);
                
            
            return new OkObjectResult("Billing information created");
        }
    
    }
    
}
