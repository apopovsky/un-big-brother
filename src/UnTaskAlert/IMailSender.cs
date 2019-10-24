namespace UnTaskAlert
{
    public interface IMailSender
    {
        void SendMessage(string subject, string body, string to);
    }
}