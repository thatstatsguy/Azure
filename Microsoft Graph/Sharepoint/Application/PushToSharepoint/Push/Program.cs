using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Azure.Identity;
using Microsoft.Graph;
using System.Net.Http.Headers;
using Azure.Core;
using Azure.Storage.Blobs;


//azure core 1.18

namespace Push
{   
    // DOCUMENTATION
    // This function must be deployed in a function app which has an application managed identity already set up with Sites.Selected permission to the applicable site
    // Original Article on using Sites.Selected to selective give access to a sharepoint site: https://developer.microsoft.com/en-us/graph/blogs/controlling-app-access-on-specific-sharepoint-site-collections/
    [StorageAccount("AzureWebJobsStorage")]
    public static class CopyFileFromBlobToSharepoint
    {
        
        private static readonly Lazy<IDictionary<string, BlobServiceClient>> _serviceClients = new Lazy<IDictionary<string, BlobServiceClient>>(() => new Dictionary<string, BlobServiceClient>());
        private static readonly Lazy<TokenCredential> _msiCredential = new Lazy<TokenCredential>(() =>
        {
            // https://docs.microsoft.com/en-us/dotnet/api/azure.identity.defaultazurecredential?view=azure-dotnet
            // Using DefaultAzureCredential allows for local dev by setting environment variables for the current user, provided said user
            // has the necessary credentials to perform the operations the MSI of the Function app needs in order to do its work. Including
            // interactive credentials will allow browser-based login when developing locally.
            return new Azure.Identity.DefaultAzureCredential(includeInteractiveCredentials: true);
        });
        
        //https://docs.microsoft.com/en-us/samples/azure-samples/functions-storage-managed-identity/using-managed-identity-between-azure-functions-and-azure-storage/
        [FunctionName("CopyFileFromBlobToSharepoint")]
        public static async Task RunAsync([BlobTrigger("testblob/{name}")] Stream myBlob, string name, ILogger log)
        {
            GraphServiceClient graphServiceClient = new GraphServiceClient(new DefaultAzureCredential());  
            log.LogInformation("Got graph client");

            
            //start streaming selected file (is capable for a large file)
            //https://docs.microsoft.com/en-us/graph/sdks/large-file-upload?tabs=csharp

            string finalFileName = "TestFolder/" + name;
            log.LogInformation(finalFileName);
            
            log.LogInformation("Starting file streaming");
            await using (var fileStream = myBlob)
            {

                // Use properties to specify the conflict behavior
                // in this case, replace
                var uploadProps = new DriveItemUploadableProperties
                {
                    ODataType = null,
                    AdditionalData = new Dictionary<string, object>
                    {
                        { "@microsoft.graph.conflictBehavior", "replace" }
                    }
                };
                

                //original article with question on how to get link to the correct drive link:
                //https://stackoverflow.com/questions/68955291/accessing-the-shared-documents-folder-within-a-private-group-of-a-sharepoint-sit
                //use graph explorer with GET https://graph.microsoft.com/v1.0/sites/devsite.sharepoint.com:/sites/Development to get id of drive item
                log.LogInformation("Creating upload session");
                try
                {
                    var uploadSession =
                    await 
                        graphServiceClient
                            .Sites["<UniqueSharepoint>.sharepoint.com"]
                            // id of the documents drive/library/folder within the sharepoint folder
                            .Drives["<Unique Drive ID>"] 
                            .Root
                            .ItemWithPath(finalFileName)
                            .CreateUploadSession(uploadProps)
                            .Request()
                            .PostAsync();

                    log.LogInformation("Upload session created");
                    
                    // Max slice size must be a multiple of 320 KiB
                    int maxSliceSize = 320 * 1024;
                    var fileUploadTask =
                        new LargeFileUploadTask<DriveItem>(uploadSession, fileStream, maxSliceSize);
                    
                    log.LogInformation("Upload task created");

                    
                    // Create a callback that is invoked after each slice is uploaded
                    IProgress<long> progress = new Progress<long>(prog => {
                        Console.WriteLine($"Uploaded {prog} bytes of {fileStream.Length} bytes");
                        log.LogInformation($"Uploaded {prog} bytes of {fileStream.Length} bytes");
                    });

                    try
                    {
                        // Upload the file
                        var uploadResult = await fileUploadTask.UploadAsync(progress);

                        if (uploadResult.UploadSucceeded)
                        {
                            // The ItemResponse object in the result represents the
                            // created item.
                            Console.WriteLine($"Upload complete, item ID: {uploadResult.ItemResponse.Id}");
                        }
                        else
                        {
                            Console.WriteLine("Upload failed");
                        }
                    }
                    catch (ServiceException ex)
                    {
                        Console.WriteLine($"Error uploading: {ex}");
                    }
                }
                catch (AuthenticationFailedException  ex)
                {
                    log.LogError(ex.Message);
                    
                    log.LogError(ex.StackTrace);
                }
                
            }
            log.LogInformation("Finished upload session");

            //return new OkObjectResult("Copy process is complete");
        }
    }
    
    //Implements the IAuthenticationProvider interface to create a method which can be used by the graph api client
    public class AzureAuthenticationProviderTest : IAuthenticationProvider
    {
        string accessToken = string.Empty;
        public AzureAuthenticationProviderTest(string accessToken) {
            this.accessToken = accessToken;

        }

        public async Task AuthenticateRequestAsync(HttpRequestMessage request)
        {
            try
            {

                // Append the access token to the request.
                request.Headers.Authorization = new AuthenticationHeaderValue("bearer", accessToken);
            }
            catch (Exception _)
            {
            }
        }
    }
}
