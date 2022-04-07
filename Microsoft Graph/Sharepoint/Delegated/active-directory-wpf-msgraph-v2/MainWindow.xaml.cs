using Microsoft.Graph;
using Microsoft.Identity.Client;
using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using Prompt = Microsoft.Identity.Client.Prompt;
using PublicClientApplication = Microsoft.Identity.Client.PublicClientApplication;
namespace active_directory_wpf_msgraph_v2
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        //Set the API Endpoint to Graph 'me' endpoint.
        // To change from Microsoft public cloud to a national cloud, use another value of graphAPIEndpoint.
        // Reference with Graph endpoints here: https://docs.microsoft.com/graph/deployments#microsoft-graph-and-graph-explorer-service-root-endpoints
        string graphAPIEndpoint = "https://graph.microsoft.com/v1.0/me";
        //Set the scope for API call to user.read
        string[] scopes = new string[] { "user.read", "files.read" };
        public MainWindow()
        {
            InitializeComponent();
        }
        /// <summary>
        /// Call AcquireToken - to acquire a token requiring user to sign-in
        /// </summary>
        private async void CallGraphButton_Click(object sender, RoutedEventArgs e)
        {
            //// Multi-tenant apps can use "common",
            //// single-tenant apps must use the tenant ID from the Azure portal
            //var tenantId = "46d4e301-e70a-4b8f-b13b-6bec241e5e6d";
            //// Values from app registration
            //var clientId = "5bea66d2-4d70-4dc7-994b-bd0465e5e18e";
            //var scopes = new[] { "User.Read" };
            //// using Azure.Identity;
            //var options = new InteractiveBrowserCredentialOptions
            //{
            //    TenantId = tenantId,
            //    ClientId = clientId,
            //    AuthorityHost = AzureAuthorityHosts.AzurePublicCloud,
            //    // MUST be http://localhost or http://localhost:PORT
            //    // See https://github.com/AzureAD/microsoft-authentication-library-for-dotnet/wiki/System-Browser-on-.Net-Core
            //    RedirectUri = new Uri("https://login.microsoftonline.com/common/oauth2/nativeclient"),
            //};
            //// https://docs.microsoft.com/dotnet/api/azure.identity.interactivebrowsercredential
            //var interactiveCredential = new InteractiveBrowserCredential(options);
            //var graphClient = new GraphServiceClient(interactiveCredential, scopes);
            //var requestAllUsers = graphClient.Users.Request();
            //var results = await requestAllUsers.GetAsync();
            //foreach (var file in results)
            //{
            //    ResultText.Text += file.Id + " : " + file.DisplayName + Environment.NewLine; ;
            //}
            //return;
            AuthenticationResult authResult = null;
            var app = App.PublicClientApp;
            ResultText.Text = string.Empty;
            TokenInfoText.Text = string.Empty;
            IAccount firstAccount;
            switch(howToSignIn.SelectedIndex)
            {
                // 0: Use account used to signed-in in Windows (WAM)
                case 0:
                    // WAM will always get an account in the cache. So if we want
                    // to have a chance to select the accounts interactively, we need to
                    // force the non-account
                    firstAccount = PublicClientApplication.OperatingSystemAccount;
                    break;
                //  1: Use one of the Accounts known by Windows(WAM)
                case 1:
                    // We force WAM to display the dialog with the accounts
                    firstAccount = null;
                    break;
                //  Use any account(Azure AD). It's not using WAM
                default:
                    var accounts = await app.GetAccountsAsync();
                    firstAccount = accounts.FirstOrDefault();
                    break;
            }
            try
            {
                authResult = await app.AcquireTokenSilent(scopes, firstAccount)
                    .ExecuteAsync();
            }
            catch (MsalUiRequiredException ex)
            {
                // A MsalUiRequiredException happened on AcquireTokenSilent.
                // This indicates you need to call AcquireTokenInteractive to acquire a token
                System.Diagnostics.Debug.WriteLine($"MsalUiRequiredException: {ex.Message}");
                try
                {
                    authResult = await app.AcquireTokenInteractive(scopes)
                        .WithAccount(firstAccount)
                        .WithParentActivityOrWindow(new WindowInteropHelper(this).Handle) // optional, used to center the browser on the window
                        .WithPrompt(Prompt.SelectAccount)
                        .ExecuteAsync();
                }
                catch (MsalException msalex)
                {
                    ResultText.Text = $"Error Acquiring Token:{System.Environment.NewLine}{msalex}";
                }
            }
            catch (Exception ex)
            {
                ResultText.Text = $"Error Acquiring Token Silently:{System.Environment.NewLine}{ex}";
                return;
            }
            if (authResult != null)
            {
                var authProvider = new DelegateAuthenticationProvider(async (request) => {
                    // Use Microsoft.Identity.Client to retrieve token
                    //var result = await app.AcquireTokenByIntegratedWindowsAuth(scopes).ExecuteAsync();
                    request.Version = HttpVersion.Version10;
                    request.Headers.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authResult.AccessToken);
                });
                var graphClient = new GraphServiceClient(authProvider);
                var requestAllUsers = graphClient.Me.Drive.Root.Children.Request();
                var results = await requestAllUsers.GetAsync();
                foreach (var file in results)
                {
                    var stream = await graphClient.Me.Drive.Items[file.Id].Content.Request().GetAsync();
                    var drf = System.IO.File.Create(@"C:\Temp\ThanksChris.xlsx");
                    stream.Seek(0, System.IO.SeekOrigin.Begin);
                    stream.CopyTo(drf);
                    stream.Flush();
                    ResultText.Text += file.Id + " : " + file.Name + Environment.NewLine; ;
                }
                ResultText.Text = await GetHttpContentWithToken(graphAPIEndpoint, authResult.AccessToken);
                DisplayBasicTokenInfo(authResult);
                this.SignOutButton.Visibility = Visibility.Visible;
            }
        }
        /// <summary>
        /// Perform an HTTP GET request to a URL using an HTTP Authorization header
        /// </summary>
        /// <param name="url">The URL</param>
        /// <param name="token">The token</param>
        /// <returns>String containing the results of the GET operation</returns>
        public async Task<string> GetHttpContentWithToken(string url, string token)
        {
            var httpClient = new System.Net.Http.HttpClient();
            System.Net.Http.HttpResponseMessage response;
            try
            {
                var request = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Get, url);
                //Add the token in Authorization header
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                response = await httpClient.SendAsync(request);
                var content = await response.Content.ReadAsStringAsync();
                return content;
            }
            catch (Exception ex)
            {
                return ex.ToString();
            }
        }
        /// <summary>
        /// Sign out the current user
        /// </summary>
        private async void SignOutButton_Click(object sender, RoutedEventArgs e)
        {
            var accounts = await App.PublicClientApp.GetAccountsAsync();
            if (accounts.Any())
            {
                try
                {
                    await App.PublicClientApp.RemoveAsync(accounts.FirstOrDefault());
                    this.ResultText.Text = "User has signed-out";
                    this.CallGraphButton.Visibility = Visibility.Visible;
                    this.SignOutButton.Visibility = Visibility.Collapsed;
                }
                catch (MsalException ex)
                {
                    ResultText.Text = $"Error signing-out user: {ex.Message}";
                }
            }
        }
        /// <summary>
        /// Display basic information contained in the token
        /// </summary>
        private void DisplayBasicTokenInfo(AuthenticationResult authResult)
        {
            TokenInfoText.Text = "";
            if (authResult != null)
            {
                TokenInfoText.Text += $"Username: {authResult.Account.Username}" + Environment.NewLine;
                TokenInfoText.Text += $"Token Expires: {authResult.ExpiresOn.ToLocalTime()}" + Environment.NewLine;
            }
        }
        private void UseWam_Changed(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            SignOutButton_Click(sender, e);
            App.CreateApplication(howToSignIn.SelectedIndex != 2); // Not Azure AD accounts (that is use WAM accounts)
        }
    }
}