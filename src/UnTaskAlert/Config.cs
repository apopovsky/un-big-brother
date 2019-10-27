namespace UnTaskAlert
{
    public class Config
    {
        public string TelegramBotKey { get; set; }
        public string FromEmailAddress { get; set; }
        public string EMailPassword { get; set; }
        public string Smtp { get; set; }
        public string AzureDevOpsAddress { get; set; }
        public string AzureDevOpsAccessToken { get; set; }
        public string Subscribers { get; set; }
        public string CosmosDbEndpointUri { get; set; }
        public string CosmosDbPrimaryKey { get; set; }
        public string EmailDomain { get; set; }
    }
}