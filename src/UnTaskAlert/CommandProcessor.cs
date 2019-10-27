using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using UnTaskAlert.Common;
using UnTaskAlert.Models;

namespace UnTaskAlert
{
    public class CommandProcessor : ICommandProcessor
    {
        private readonly IReportingService _service;
        private readonly INotifier _notifier;
        private readonly Config _config;
        private readonly IDbAccessor _dbAccessor;

        public CommandProcessor(INotifier notifier, IReportingService service, IDbAccessor dbAccessor, IOptions<Config> options)
        {
            _notifier = Arg.NotNull(notifier, nameof(notifier));
            _service = Arg.NotNull(service, nameof(service));
            _config = Arg.NotNull(options.Value, nameof(options));
            _dbAccessor = Arg.NotNull(dbAccessor, nameof(dbAccessor));
        }

        public async Task Process(Update update, ILogger log)
        {
            if (update.Type != UpdateType.Message)
            {
                return;
            }

            log.LogInformation($"CommandProcessor.Process(): {update.Message.Text}");

            var startDate = DateTime.UtcNow.Date;

            // this is a naive and quick implementation
            // proper command parsing is required
            if (update.Message.Text.StartsWith("/email"))
            {
                await SetEmailAddress(update);
            }
            else if (update.Message.Text.StartsWith("/week"))
            {
                startDate = StartOfWeek(DateTime.UtcNow, DayOfWeek.Monday);
                await CreateWorkHoursReport(update, log, startDate);
            }
            else if (update.Message.Text.StartsWith("/month"))
            {
                startDate = new DateTime(DateTime.UtcNow.Date.Year, DateTime.UtcNow.Date.Month, 1);
                await CreateWorkHoursReport(update, log, startDate);
            }
            else if (update.Message.Text.StartsWith("/day"))
            {
                startDate = DateTime.UtcNow.Date;
                await CreateWorkHoursReport(update, log, startDate);
            }
            else if (update.Message.Text.StartsWith("/active"))
            {
                await CreateActiveTasksReport(update, log, startDate);
            }
            else
            {
                var to = new Subscriber
                {
                    TelegramId = update.Message.Chat.Id.ToString()
                };
                await _notifier.Instruction(to);
            }
        }

        private async Task SetEmailAddress(Update update)
        {
            var email = update.Message.Text.Substring(6).Trim();

            if (string.IsNullOrWhiteSpace(email) || !email.EndsWith(_config.EmailDomain))
            {
                await _notifier.IncorrectEmail(update.Message.Chat.Id.ToString());

                return;
            }

            var existingSubscriber = await _dbAccessor.GetSubscriberById(update.Message.Chat.Id.ToString());
            var subscriber = new Subscriber
            {
                Email = email,
                TelegramId = update.Message.Chat.Id.ToString(),
                StartWorkingHoursUtc = existingSubscriber?.StartWorkingHoursUtc ?? default,
                EndWorkingHoursUtc = existingSubscriber?.EndWorkingHoursUtc ?? default
            };
            await _dbAccessor.AddOrUpdateSubscriber(subscriber);

            await _notifier.EmailUpdated(subscriber);
        }

        private async Task CreateActiveTasksReport(Update update, ILogger log, DateTime startDate)
        {
            var subscriber = await _dbAccessor.GetSubscriberById(update.Message.Chat.Id.ToString());
            if (subscriber == null)
            {
                return;
            }

            await _service.ActiveTasksReport(subscriber,
                _config.AzureDevOpsAddress,
                _config.AzureDevOpsAccessToken,
                startDate,
                log);
        }

        private async Task CreateWorkHoursReport(Update update, ILogger log, DateTime startDate)
        {
            var subscriber = await _dbAccessor.GetSubscriberById(update.Message.Chat.Id.ToString());
            if (subscriber == null)
            {
                return;
            }

            await _service.CreateWorkHoursReport(subscriber,
                _config.AzureDevOpsAddress,
                _config.AzureDevOpsAccessToken,
                startDate,
                log);
        }

        private static DateTime StartOfWeek(DateTime dt, DayOfWeek startOfWeek)
        {
            int diff = (7 + (dt.DayOfWeek - startOfWeek)) % 7;
            return dt.AddDays(-1 * diff).Date;
        }
    }
}
