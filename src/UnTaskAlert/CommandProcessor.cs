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
        private readonly IPinGenerator _pinGenerator;

        private static readonly int MaxVerificationAttempts = 3;
        private static readonly Parser Parser = Parser.Default;
        private static readonly int PauseBeforeAnswer = 1000;

        public CommandProcessor(INotifier notifier,
            IReportingService service,
            IDbAccessor dbAccessor,
            IMailSender mailSender,
            IPinGenerator pinGenerator,
            IOptions<Config> options)
        {
            _notifier = Arg.NotNull(notifier, nameof(notifier));
            _service = Arg.NotNull(service, nameof(service));
            _config = Arg.NotNull(options.Value, nameof(options));
            _dbAccessor = Arg.NotNull(dbAccessor, nameof(dbAccessor));
            _mailSender = Arg.NotNull(mailSender, nameof(mailSender));
            _pinGenerator = Arg.NotNull(pinGenerator, nameof(pinGenerator));
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
                log.LogInformation($"Process: Subscriber is 'null'");
            }
            else
            {
                log.LogInformation($"TelegramId: {subscriber.TelegramId}{Environment.NewLine}" +
                                   $"VerificationAttempts: {subscriber.VerificationAttempts}{Environment.NewLine}" +
                                   $"PIN: {subscriber.Pin}{Environment.NewLine}" +
                                   $"ExpectedAction: {subscriber.ExpectedAction}{Environment.NewLine}" +
                                   $"Email: {subscriber.Email}{Environment.NewLine}" +
                                   $"Working hours (UTC): {subscriber.StartWorkingHoursUtc}-{subscriber.EndWorkingHoursUtc}{Environment.NewLine}" +
                                   $"Is account verified: {subscriber.IsVerified}{Environment.NewLine}" +
                                   $"Hours per day: {subscriber.HoursPerDay}{Environment.NewLine}" +
                                   $"LastNoActiveTasksAlert: {subscriber.LastNoActiveTasksAlert}{Environment.NewLine}" +
                                   $"LastMoreThanSingleTaskIsActiveAlert: {subscriber.LastMoreThanSingleTaskIsActiveAlert}{Environment.NewLine}" +
                                   $"LastActiveTaskOutsideOfWorkingHoursAlert: {subscriber.LastActiveTaskOutsideOfWorkingHoursAlert}{Environment.NewLine}");
            }
            
            if (subscriber == null)
            {
                await NewUserFlow(log, chatId);
                return;
            }

            if (subscriber.ExpectedAction == ExpectedActionType.ExpectedEmail)
            {
                await SetEmailFlow(log, subscriber, input);
                return;
            }

            if (!subscriber.IsVerified && subscriber.ExpectedAction == ExpectedActionType.ExpectedPin)
            {
                await NotVerifiedUserFlow(log, subscriber, input);
                return;
            }

            if (subscriber.ExpectedAction == ExpectedActionType.VerifiedSubscriberCommand)
            {
                await VerifiedUserFlow(log, subscriber, update, input);
            }

            throw new InvalidOperationException($"The bot is lost and doesn't know what to do. chatId '{subscriber.TelegramId}', expected action '{subscriber.ExpectedAction}'");
        }

        private async Task SetEmailFlow(ILogger log, Subscriber subscriber, string input)
        {
            log.LogInformation($"SetEmailFlow() is executed for chatId '{subscriber.TelegramId}', input '{input}'");
            await Task.Delay(PauseBeforeAnswer);

            if (string.IsNullOrWhiteSpace(input) || !input.EndsWith(_config.EmailDomain))
            {
                await _notifier.IncorrectEmail(subscriber.TelegramId);

                return;
            }

            subscriber.Email = input;
            subscriber.IsVerified = false;
            subscriber.Pin = _pinGenerator.GetRandomPin();
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
            await Task.Delay(PauseBeforeAnswer);

            var subscriber = new Subscriber
            {
                Email = string.Empty,
                TelegramId = chatId,
                StartWorkingHoursUtc =  default,
                EndWorkingHoursUtc =  default,
                HoursPerDay =  ReportingService.HoursPerDay,
                IsVerified = false,
                Pin = _pinGenerator.GetRandomPin(),
                VerificationAttempts = default,
                ExpectedAction = ExpectedActionType.ExpectedEmail
            };
            await _dbAccessor.AddOrUpdateSubscriber(subscriber);

            await _notifier.RequestEmail(chatId);
        }

        private async Task NotVerifiedUserFlow(ILogger log, Subscriber subscriber, string input)
        {
            log.LogInformation($"NotVerifiedUserFlow() is executed for chatId '{subscriber.TelegramId}'");
            await Task.Delay(PauseBeforeAnswer);

            var isNumeric = int.TryParse(input, out int code);
            if (isNumeric && subscriber.ExpectedAction == ExpectedActionType.ExpectedPin)
            {
                await VerifyAccount(log, subscriber, code);
                return;
            }

            await Parser.ParseArguments<Email, Info, Delete>(input.Split(" "))
                .MapResult(
                    async (Email opts) => await ResetEmail(log, subscriber),
                    async (Info opts) => await Info(log, subscriber),
                    async (Delete opts) => await Delete(log, subscriber),
                    async errs => await _notifier.CouldNotVerifyAccount(subscriber));
        }

        private async Task VerifiedUserFlow(ILogger log, Subscriber subscriber, Update update, string input)
        {
            log.LogInformation($"VerifiedUserFlow() is executed for chatId '{subscriber.TelegramId}', input: '{input}'");

            await Parser.ParseArguments<Day, Week, Month, Standup, Email, Active, Info, Healthcheck, Delete>(input.Split(" "))
                .MapResult(
                    async (Day opts) => await CreateWorkHoursReport(log, subscriber, DateTime.Today),
                    async (Week opts) => await CreateWorkHoursReport(log, subscriber, DateUtils.StartOfWeek()),
                    async (Month opts) => await CreateWorkHoursReport(log, subscriber, DateUtils.StartOfMonth()),
                    async (Standup opts) => await CreateStandUpReport(log, subscriber),
                    async (Email opts) => await ResetEmail(log, subscriber),
                    async (Active opts) => await CreateActiveTasksReport(log, subscriber, DateTime.Today),
                    async (Info opts) => await Info(log, subscriber),
                    async (Healthcheck opts) =>
                    {
                        double threshold = 0;
                        if (update.Message.Text.Length > HealthcheckCommand.Length)
                        {
                            double.TryParse(update.Message.Text.Substring(HealthcheckCommand.Length), out threshold);
                        }

                        await CreateHealthCheckReport(log, subscriber, DateUtils.StartOfMonth(), threshold);
                    },
                    async (Delete opts) => await Delete(log, subscriber),
                    async errs =>
                    {
                        var to = new Subscriber
                        {
                            TelegramId = update.Message.Chat.Id.ToString()
                        };
                        await _notifier.Instruction(to);
                    });
        }

        private async Task Info(ILogger log, Subscriber subscriber)
        {
            log.LogInformation($"Information for subscriber '{subscriber.TelegramId}'");
            await _notifier.AccountInfo(subscriber);
        }

        private async Task Delete(ILogger log, Subscriber subscriber)
        {
            log.LogInformation($"Deleting subscriber '{subscriber.TelegramId}'");
            await _dbAccessor.DeleteIfExists(subscriber);
        }

        private async Task ResetEmail(ILogger log, Subscriber subscriber)
        {
            log.LogInformation($"Resetting email for subscriber '{subscriber.TelegramId}'");

            // we are not updating Attempts for security reasons
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

            if (subscriber.Pin == code && subscriber.VerificationAttempts <= MaxVerificationAttempts)
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
    }
}
