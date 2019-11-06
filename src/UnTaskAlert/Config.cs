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
        public string CosmosDbConnectionString { get; set; }
        public string EmailDomain { get; set; }
    }
}