using System;
using System.Threading.Tasks;
using CommandLine;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using UnTaskAlert.Commands;
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
        private static readonly Parser Parser = Parser.Default;
        private static readonly int PauseBeforeAnswer = 1000;

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

            var input = update.Message?.Text;
            if (string.IsNullOrWhiteSpace(input))
            {
                return;
            }

            var chatId = update.Message.Chat.Id.ToString();

            log.LogInformation($"Processing the command: {update.Message.Text}");
            await _notifier.Typing(update.Message.Chat.Id.ToString());

            var subscriber = await _dbAccessor.GetSubscriberById(update.Message.Chat.Id.ToString());
            if (subscriber == null)
            {
                await Task.Delay(PauseBeforeAnswer);
                await NewUserFlow(log, chatId);
                return;
            }

            if (subscriber.ExpectedAction == ExpectedActionType.ExpectedEmail)
            {
                await Task.Delay(PauseBeforeAnswer);
                await SetEmailFlow(log, input, subscriber);
                return;
            }

            if (!subscriber.IsVerified && subscriber.ExpectedAction == ExpectedActionType.ExpectedPin)
            {
                await NotVerifiedUserFlow(log, input, subscriber);
                return;
            }

            if (subscriber.ExpectedAction == ExpectedActionType.VerifiedSubscriberCommand)
            {
                await VerifiedUserFlow(update, log, input, subscriber);
            }

            log.LogWarning($"The bot is lost and doesn't know what to do. chatId '{subscriber.TelegramId}', expected action '{subscriber.ExpectedAction}'");
        }

        private async Task SetEmailFlow(ILogger log, string input, Subscriber subscriber)
        {
            log.LogInformation($"SetEmailFlow() is executed for chatId '{subscriber.TelegramId}', input '{input}'");

            if (string.IsNullOrWhiteSpace(input) || !input.EndsWith(_config.EmailDomain))
            {
                await _notifier.IncorrectEmail(subscriber.TelegramId);

                return;
            }

            subscriber.Email = input;
            subscriber.IsVerified = false;
            subscriber.Pin = GetRandomPin();
            subscriber.ExpectedAction = ExpectedActionType.ExpectedPin;
            await _dbAccessor.AddOrUpdateSubscriber(subscriber);

            await _notifier.EmailUpdated(subscriber);

            _mailSender.SendMessage("UN Big Brother bot verification code",
                $"Please send the following PIN to the bot through the chat: {subscriber.Pin}",
                subscriber.Email);
        }

        private async Task NewUserFlow(ILogger log, string chatId)
        {
            log.LogInformation($"NewUserFlow() is executed for chatId '{chatId}'");

            var subscriber = new Subscriber
            {
                Email = string.Empty,
                TelegramId = chatId,
                StartWorkingHoursUtc =  default,
                EndWorkingHoursUtc =  default,
                HoursPerDay =  ReportingService.HoursPerDay,
                IsVerified = false,
                Pin = GetRandomPin(),
                VerificationAttempts = default,
                ExpectedAction = ExpectedActionType.ExpectedEmail
            };
            await _dbAccessor.AddOrUpdateSubscriber(subscriber);

            await _notifier.RequestEmail(chatId);
        }

        private async Task NotVerifiedUserFlow(ILogger log, string input, Subscriber subscriber)
        {
            log.LogInformation($"NotVerifiedUserFlow() is executed for chatId '{subscriber.TelegramId}'");

            var isNumeric = int.TryParse(input, out int code);
            if (isNumeric && subscriber.ExpectedAction == ExpectedActionType.ExpectedPin)
            {
                await VerifyAccount(log, subscriber, code);
                return;
            }

            await Parser.ParseArguments<Email>(input.Split(" "))
                .MapResult(
                    async opts => await ResetEmail(log, subscriber),
                    async errs => await _notifier.Instruction(subscriber));
        }

        private async Task VerifiedUserFlow(Update update, ILogger log, string input, Subscriber subscriber)
        {
            log.LogInformation($"VerifiedUserFlow() is executed for chatId '{subscriber.TelegramId}', input: '{input}'");

            await Parser.ParseArguments<Day, Week, Month, Standup, Email, Active, Healthcheck>(input.Split(" "))
                .MapResult(
                    async (Day opts) => await CreateWorkHoursReport(log, subscriber, DateTime.Today),
                    async (Week opts) => await CreateWorkHoursReport(log, subscriber, StartOfWeek()),
                    async (Month opts) => await CreateWorkHoursReport(log, subscriber, StartOfMonth()),
                    async (Standup opts) => await CreateStandUpReport(log, subscriber),
                    async (Email opts) => await ResetEmail(log, subscriber),
                    async (Active opts) => await CreateActiveTasksReport(log, subscriber, DateTime.Today),
                    async (Healthcheck opts) =>
                    {
                        double threshold = 0;
                        if (update.Message.Text.Length > HealthcheckCommand.Length)
                        {
                            double.TryParse(update.Message.Text.Substring(HealthcheckCommand.Length), out threshold);
                        }

                        await CreateHealthCheckReport(log, subscriber, StartOfMonth(), threshold);
                    },
                    async errs =>
                    {
                        var to = new Subscriber
                        {
                            TelegramId = update.Message.Chat.Id.ToString()
                        };
                        await _notifier.Instruction(to);
                    });
        }

        private async Task ResetEmail(ILogger log, Subscriber subscriber)
        {
            log.LogInformation($"Resetting email for subscriber '{subscriber.TelegramId}'");

            subscriber.Email = string.Empty;
            subscriber.ExpectedAction = ExpectedActionType.ExpectedEmail;
            subscriber.IsVerified = false;
            await _dbAccessor.AddOrUpdateSubscriber(subscriber);
            await _notifier.RequestEmail(subscriber.TelegramId);
        }

        private async Task CreateStandUpReport(ILogger log, Subscriber subscriber)
        {
            await _service.CreateStandupReport(subscriber,
                _config.AzureDevOpsAddress,
                _config.AzureDevOpsAccessToken,
                log);
        }

        private async Task VerifyAccount(ILogger log, Subscriber subscriber, int code)
        {
            log.LogInformation($"Verifying account for {subscriber.Email}, entered pin is '{code}'.");
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
                subscriber.ExpectedAction = ExpectedActionType.VerifiedSubscriberCommand;
                await _notifier.AccountVerified(subscriber);
            }
            else
            {
                await _notifier.CouldNotVerifyAccount(subscriber);
            }

            await _dbAccessor.AddOrUpdateSubscriber(subscriber);
        }

        private async Task CreateActiveTasksReport(ILogger log, Subscriber subscriber, DateTime startDate)
        {
            await _service.ActiveTasksReport(subscriber,
                _config.AzureDevOpsAddress,
                _config.AzureDevOpsAccessToken,
                startDate,
                log);
        }

        private async Task CreateWorkHoursReport(ILogger log, Subscriber subscriber, DateTime startDate)
        {
            await _service.CreateWorkHoursReport(subscriber,
                _config.AzureDevOpsAddress,
                _config.AzureDevOpsAccessToken,
                startDate,
                log);
        }

        private async Task CreateHealthCheckReport(ILogger log, Subscriber subscriber, DateTime startDate, double threshold)
		{
            await _service.CreateHealthCheckReport(subscriber,
                _config.AzureDevOpsAddress,
                _config.AzureDevOpsAccessToken,
                startDate,
                threshold,
                log);
		}

        private static DateTime StartOfWeek()
        {
            var dt = DateTime.Today;
            int diff = (7 + (dt.DayOfWeek - DayOfWeek.Monday)) % 7;

            return dt.AddDays(-1 * diff).Date;
        }

        private static DateTime StartOfMonth()
        {
            return new DateTime(DateTime.Today.Date.Year, DateTime.UtcNow.Date.Month, 1);
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
