using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using UnTaskAlert.Common;

namespace UnTaskAlert
{
    public class MailSender : IMailSender
    {
        private readonly Config _config;

        public MailSender(IOptions<Config> options)
        {
            _config = Arg.NotNull(options.Value, nameof(options));
        }

        public void SendMessage(string subject, string body, string to)
        {
            var fromAddress = new MailAddress(_config.FromEmailAddress);
            var toAddress = new MailAddress(to);

            using (SmtpClient smtp = CreateSmtpClient(fromAddress))
            using (var message = CreateMessage(subject, body, fromAddress, toAddress))
            {
                smtp.Send(message);
            }
        }

        private static MailMessage CreateMessage(string subject, string body, MailAddress fromAddress, MailAddress toAddress)
        {
            return new MailMessage(fromAddress, toAddress)
            {
                Subject = subject,
                Body = body
            };
        }

        private SmtpClient CreateSmtpClient(MailAddress fromAddress)
        {
            return new SmtpClient
            {
                Host = _config.Smtp,
                Port = 587,
                EnableSsl = true,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(fromAddress.Address, _config.EMailPassword)
            };
        }
    }
}