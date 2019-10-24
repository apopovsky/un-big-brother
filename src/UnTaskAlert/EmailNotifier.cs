using System.Threading.Tasks;
using UnTaskAlert.Common;
using UnTaskAlert.Models;

namespace UnTaskAlert
{
    public class EmailNotifier : INotifier
    {
        private readonly IMailSender _mailSender;

        public EmailNotifier(IMailSender mailSender)
        {
            _mailSender = Arg.NotNull(mailSender, nameof(mailSender));
        }

        public async Task NoActiveTasksDuringWorkingHours(Subscriber subscriber)
        {
            // todo
        }

        public async Task ActiveTaskOutsideOfWorkingHours(Subscriber subscriber)
        {
            // todo
        }

        public async Task MoreThanSingleTaskIsActive(Subscriber subscriber)
        {
            // todo
        }

        public async Task Ping(Subscriber subscriber)
        {
            // todo
        }
    }
}
