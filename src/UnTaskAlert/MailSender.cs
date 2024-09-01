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

            using var smtp = CreateSmtpClient(fromAddress);
            using var message = CreateMessage(subject, body, fromAddress, toAddress);
            smtp.Send(message);
        }

        public void SendHtmlMessage(string subject, string body, string to)
        {
            var fromAddress = new MailAddress(_config.FromEmailAddress);
            var toAddress = new MailAddress(to);

            using var smtp = CreateSmtpClient(fromAddress);
            using var message = CreateMessage(subject, body, fromAddress, toAddress, isHtml: true);
            smtp.Send(message);
        }

        private static MailMessage CreateMessage(string subject, string body, 
            MailAddress fromAddress, MailAddress toAddress, bool isHtml = false) =>
            new(fromAddress, toAddress)
            {
                Subject = subject,
                Body = body,
                IsBodyHtml = isHtml
            };

        private SmtpClient CreateSmtpClient(MailAddress fromAddress) =>
            new()
            {
                Host = _config.Smtp,
                Port = 587,
                EnableSsl = true,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(_config.EMailUser, _config.EMailPassword)
            };
    }
}