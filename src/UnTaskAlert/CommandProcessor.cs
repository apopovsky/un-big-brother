using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using UnTaskAlert.Commands.Workflow;
using UnTaskAlert.Common;
using UnTaskAlert.Models;

namespace UnTaskAlert
{
    public class CommandProcessor : ICommandProcessor
    {
        private readonly INotifier _notifier;
        private readonly Config _config;
        private readonly IDbAccessor _dbAccessor;
        private readonly IPinGenerator _pinGenerator;
        private readonly IServiceProvider _serviceProvider;

        private static readonly int PauseBeforeAnswer = 1000;

        public CommandProcessor(INotifier notifier,
            IDbAccessor dbAccessor,
            IPinGenerator pinGenerator,
            IServiceProvider serviceProvider,
            IOptions<Config> options)
        {
            _notifier = Arg.NotNull(notifier, nameof(notifier));
            _config = Arg.NotNull(options.Value, nameof(options));
            _dbAccessor = Arg.NotNull(dbAccessor, nameof(dbAccessor));
            _pinGenerator = Arg.NotNull(pinGenerator, nameof(pinGenerator));
            _serviceProvider = Arg.NotNull(serviceProvider, nameof(serviceProvider));
        }
        
        public async Task Process(Update update, ILogger log, CancellationToken cancellationToken)
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
            await _notifier.Typing(update.Message.Chat.Id.ToString(), cancellationToken);

            var subscriber = await _dbAccessor.GetSubscriberById(update.Message.Chat.Id.ToString(), log);

            if (subscriber == null)
            {
                log.LogInformation("Process: Subscriber is 'null'");
            }
            else
            {
                log.LogInformation($"TelegramId: {subscriber.TelegramId}{Environment.NewLine}" +
                                   $"VerificationAttempts: {subscriber.VerificationAttempts}{Environment.NewLine}" +
                                   $"PIN: {subscriber.Pin}{Environment.NewLine}" +
                                   $"Email: {subscriber.Email}{Environment.NewLine}" +
                                   $"Working hours (UTC): {subscriber.StartWorkingHoursUtc}-{subscriber.EndWorkingHoursUtc}{Environment.NewLine}" +
                                   $"Is account verified: {subscriber.IsVerified}{Environment.NewLine}" +
                                   $"Hours per day: {subscriber.HoursPerDay}{Environment.NewLine}" +
                                   $"LastNoActiveTasksAlert: {subscriber.LastNoActiveTasksAlert}{Environment.NewLine}" +
                                   $"LastMoreThanSingleTaskIsActiveAlert: {subscriber.LastMoreThanSingleTaskIsActiveAlert}{Environment.NewLine}" +
                                   $"LastActiveTaskOutsideOfWorkingHoursAlert: {subscriber.LastActiveTaskOutsideOfWorkingHoursAlert}{Environment.NewLine}");
            }

            subscriber ??= await NewUserFlow(log, chatId, cancellationToken);

            if (subscriber.ActiveWorkflow is { IsExpired: false })
            {
                var result = await subscriber.ActiveWorkflow.Step(input, subscriber,  update.Message.Chat.Id, cancellationToken);
                if (result == WorkflowResult.Finished)
                {
                    subscriber.ActiveWorkflow = null;
                }
                await _dbAccessor.AddOrUpdateSubscriber(subscriber, cancellationToken);
                return;
            }

            // todo: it would be good to use DI to create these instances
            var workflows = new CommandWorkflow[]
            {
                new SnoozeAlertWorkflow(),
                new SetSettingsWorkflow(),
                new ActiveWorkflow(),
                new StandupWorkflow(),
                new DayWorkflow(),
                new WeekWorkflow(),
                new MonthWorkflow(),
                new YearWorkflow(),
                new HealthcheckWorkflow(),
                new InfoWorkflow(),
                new AddTimeOff(),
                new DeleteWorkflow(),
                new AccountWorkflow()
            };
            var commandWorkflow = ProcessInput(log, input, workflows);

            if (commandWorkflow == null)
                throw new InvalidOperationException(
                    $"The bot is lost and doesn't know what to do. chatId '{subscriber.TelegramId}'.");
            
            var workflowResult = await commandWorkflow.Step(input, subscriber, update.Message.Chat.Id, cancellationToken);
            subscriber.ActiveWorkflow = workflowResult == WorkflowResult.Finished ? null : commandWorkflow;
            await _dbAccessor.AddOrUpdateSubscriber(subscriber, cancellationToken);

            return;

        }

        private async Task<Subscriber> NewUserFlow(ILogger log, string chatId, CancellationToken cancellationToken)
        {
            log.LogInformation($"NewUserFlow() is executed for chatId '{chatId}'");
            await Task.Delay(PauseBeforeAnswer, cancellationToken);
            var workflow = new AccountWorkflow();
            workflow.Inject(_serviceProvider, _config, log);

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
                ActiveWorkflow = workflow
            };

            await _dbAccessor.AddOrUpdateSubscriber(subscriber, cancellationToken);

            return subscriber;
        }

        private CommandWorkflow ProcessInput(ILogger logger, string input, params CommandWorkflow[] workflows)
        {
            // todo: it would be nice to have a factory for workflows, maybe
            foreach (var commandWorkflow in workflows)
            {
                if (commandWorkflow.Accepts(input))
                {
                    commandWorkflow.Inject(_serviceProvider, _config, logger);
                    return commandWorkflow;
                }
            }

            return null;
        }
    }
}
