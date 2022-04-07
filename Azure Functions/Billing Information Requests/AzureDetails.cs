namespace BillingRequest
{
    public static class AzureDetails
    {
        //only used for service principle applications
        public static string ClientID = "<client ID>";
        public static string ClientSecret = "<Client Secret>";
        public static string TenantID = "<Tenant ID>";

        public static string AccessToken { get; internal set; }
    }
}