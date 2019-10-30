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
        private const string HealthcheckCommand = "/healthcheck";
        private readonly IReportingService _service;
        private readonly INotifier _notifier;
        private readonly Config _config;
        private readonly IDbAccessor _dbAccessor;
        private readonly IMailSender _mailSender;

        private static readonly Random Random = new Random();
        private static readonly int MaxVerificationAttempts = 5;

        public CommandProcessor(INotifier notifier,
            IReportingService service,
            IDbAccessor dbAccessor,
            IMailSender mailSender,
            IOptions<Config> options)
        {
            _notifier = Arg.NotNull(notifier, nameof(notifier));
            _service = Arg.NotNull(service, nameof(service));
            _config = Arg.NotNull(options.Value, nameof(options));
            _dbAccessor = Arg.NotNull(dbAccessor, nameof(dbAccessor));
            _mailSender = Arg.NotNull(mailSender, nameof(mailSender));
        }

        public async Task Process(Update update, ILogger log)
        {
            if (update.Type != UpdateType.Message)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(update.Message?.Text))
            {
                return;
            }

            log.LogInformation($"Processing the command: {update.Message.Text}");
            await _notifier.Typing(update.Message.Chat.Id.ToString());

            var startDate = DateTime.UtcNow.Date;

            // this is a naive and quick implementation
            // proper command parsing is required
            var isNumeric = int.TryParse(update.Message.Text, out int code);
            if (isNumeric)
            {
                await VerifyAccount(update, code);
            }
            else if (update.Message.Text.StartsWith("/email"))
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
                startDate = StartOfMonth();
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
            else if (update.Message.Text.StartsWith(HealthcheckCommand))
            {
                double threshold = 0;
                if (update.Message.Text.Length > HealthcheckCommand.Length)
                {
                    double.TryParse(update.Message.Text.Substring(HealthcheckCommand.Length), out threshold);
                }
				await CreateHealthCheckReport(update, log, StartOfMonth(), threshold);
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

        private async Task VerifyAccount(Update update, int code)
        {
            // trying to verify
            var subscriber = await _dbAccessor.GetSubscriberById(update.Message.Chat.Id.ToString());
            if (subscriber.IsVerified)
            {
                // no need to verify anything
                return;
            }

            subscriber.VerificationAttempts++;

            if (subscriber.Pin == code && subscriber.VerificationAttempts < MaxVerificationAttempts)
            {
                subscriber.IsVerified = true;
                subscriber.VerificationAttempts = 0;
                await _notifier.AccountVerified(subscriber);
            }
            else
            {
                await _notifier.CouldNotVerifyAccount(subscriber);
            }

            await _dbAccessor.AddOrUpdateSubscriber(subscriber);
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
                EndWorkingHoursUtc = existingSubscriber?.EndWorkingHoursUtc ?? default,
                HoursPerDay = existingSubscriber?.HoursPerDay ?? ReportingService.HoursPerDay,
                IsVerified = false,
                Pin = GetRandomPin(),
                VerificationAttempts = existingSubscriber?.VerificationAttempts ?? default
            };
            await _dbAccessor.AddOrUpdateSubscriber(subscriber);

            _mailSender.SendMessage("UN Big Brother bot verification code",
                $"Please send the following PIN to the bot through the chat: {subscriber.Pin}",
                subscriber.Email);
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
                await _notifier.NoEmail(update.Message.Chat.Id.ToString());
                return;
            }

            if (!subscriber.IsVerified)
            {
                log.LogInformation($"{subscriber.Email} is not verified. No reports will be sent.");
                return;
            }

            await _service.CreateWorkHoursReport(subscriber,
                _config.AzureDevOpsAddress,
                _config.AzureDevOpsAccessToken,
                startDate,
                log);
        }

        private async Task CreateHealthCheckReport(Update update, ILogger log, DateTime startDate, double threshold)
		{
			var subscriber = await _dbAccessor.GetSubscriberById(update.Message.Chat.Id.ToString());
            if (subscriber == null)
            {
                await _notifier.NoEmail(update.Message.Chat.Id.ToString());
                return;
            }

            if (!subscriber.IsVerified)
            {
                log.LogInformation($"{subscriber.Email} is not verified. No reports will be sent.");
                return;
            }

            await _service.CreateHealthCheckReport(subscriber,
                _config.AzureDevOpsAddress,
                _config.AzureDevOpsAccessToken,
                startDate,
                threshold,
                log);
		}

        private static DateTime StartOfWeek(DateTime dt, DayOfWeek startOfWeek)
        {
            int diff = (7 + (dt.DayOfWeek - startOfWeek)) % 7;
            return dt.AddDays(-1 * diff).Date;
        }

        private static DateTime StartOfMonth()
        {
            return new DateTime(DateTime.UtcNow.Date.Year, DateTime.UtcNow.Date.Month, 1);
        }

        public static int GetRandomPin()
        {
            lock (Random)
            {
                return Random.Next(1000, 9999);
            }
        }
    }
}
