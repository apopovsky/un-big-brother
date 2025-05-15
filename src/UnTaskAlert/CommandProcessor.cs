using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using UnTaskAlert.Commands.Workflow;
using UnTaskAlert.Common;
using UnTaskAlert.Models;

namespace UnTaskAlert;

public class CommandProcessor(
    INotifier notifier,
    IDbAccessor dbAccessor,
    IPinGenerator pinGenerator,
    IOptions<Config> options,
    IServiceScopeFactory scopeFactory,
    ILoggerFactory loggerFactory)
    : ICommandProcessor
{
    private readonly INotifier _notifier = Arg.NotNull(notifier, nameof(notifier));
    private readonly Config _config = Arg.NotNull(options.Value, nameof(options));
    private readonly IDbAccessor _dbAccessor = Arg.NotNull(dbAccessor, nameof(dbAccessor));
    private readonly IPinGenerator _pinGenerator = Arg.NotNull(pinGenerator, nameof(pinGenerator));
    private readonly IServiceScopeFactory _scopeFactory = Arg.NotNull(scopeFactory, nameof(scopeFactory));
    private readonly ILogger<CommandProcessor> _logger = loggerFactory.CreateLogger<CommandProcessor>();

    private const int PauseBeforeAnswer = 1000;


    // Inside the CommandProcessor class constructor, add the following code

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

        _logger.LogInformation("Processing the command: {MessageText}", update.Message.Text);
        await _notifier.Typing(update.Message.Chat.Id.ToString(), cancellationToken);

        var subscriber = await _dbAccessor.GetSubscriberById(update.Message.Chat.Id.ToString(), _logger);

        if (subscriber == null)
        {
            _logger.LogInformation("Process: Subscriber is 'null'");
        }
        else
        {
            _logger.LogInformation("TelegramId: {TelegramId}\n" +
                                   "VerificationAttempts: {VerificationAttempts}\n" +
                                   "PIN: {Pin}\n" +
                                   "Email: {Email}\n" +
                                   "Working hours (UTC): {StartWorkingHoursUtc}-{EndWorkingHoursUtc}\n" +
                                   "Is account verified: {IsVerified}\n" +
                                   "Hours per day: {HoursPerDay}\n" +
                                   "LastNoActiveTasksAlert: {LastNoActiveTasksAlert}\n" +
                                   "LastMoreThanSingleTaskIsActiveAlert: {LastMoreThanSingleTaskIsActiveAlert}\n" +
                                   "LastActiveTaskOutsideOfWorkingHoursAlert: {LastActiveTaskOutsideOfWorkingHoursAlert}",
                                   subscriber.TelegramId,
                                   subscriber.VerificationAttempts,
                                   subscriber.Pin,
                                   subscriber.Email,
                                   subscriber.StartWorkingHoursUtc,
                                   subscriber.EndWorkingHoursUtc,
                                   subscriber.IsVerified,
                                   subscriber.HoursPerDay,
                                   subscriber.LastNoActiveTasksAlert,
                                   subscriber.LastMoreThanSingleTaskIsActiveAlert,
                                   subscriber.LastActiveTaskOutsideOfWorkingHoursAlert);
        }

        subscriber ??= await NewUserFlow(chatId, cancellationToken);

        if (subscriber.ActiveWorkflow is { IsExpired: false } || input.StartsWith("/abort"))
        {
            var result = await subscriber.ActiveWorkflow.Step(input, subscriber, update.Message.Chat.Id, cancellationToken);
            if (result == WorkflowResult.Finished)
            {
                subscriber.ActiveWorkflow = null;
            }
            await _dbAccessor.AddOrUpdateSubscriber(subscriber, cancellationToken);
            return;
        }

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
            new AccountWorkflow(),
            new StoryInfoWorkflow(),
        };
        var commandWorkflow = ProcessInput(input, workflows);

        if (commandWorkflow == null)
            throw new InvalidOperationException($"The bot is lost and doesn't know what to do. chatId '{subscriber.TelegramId}'.");

        var workflowResult = await commandWorkflow.Step(input, subscriber, update.Message.Chat.Id, cancellationToken);
        subscriber.ActiveWorkflow = workflowResult == WorkflowResult.Finished ? null : commandWorkflow;
        await _dbAccessor.AddOrUpdateSubscriber(subscriber, cancellationToken);
    }

    private async Task<Subscriber> NewUserFlow(string chatId, CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger<Subscriber>();
        logger.LogInformation("NewUserFlow() is executed for chatId '{ChatId}'", chatId);
        await Task.Delay(PauseBeforeAnswer, cancellationToken);
        var workflow = new AccountWorkflow();
        workflow.Inject(_scopeFactory, _config, loggerFactory);

        var subscriber = new Subscriber
        {
            Email = string.Empty,
            TelegramId = chatId,
            StartWorkingHoursUtc = TimeSpan.Zero,
            EndWorkingHoursUtc = TimeSpan.Zero,
            HoursPerDay = ReportingService.HoursPerDay,
            IsVerified = false,
            Pin = _pinGenerator.GetRandomPin(),
            VerificationAttempts = 0,
            ActiveWorkflow = workflow,
        };

        await _dbAccessor.AddOrUpdateSubscriber(subscriber, cancellationToken);

        return subscriber;
    }

    private CommandWorkflow ProcessInput(string input, params CommandWorkflow[] workflows)
    {
        foreach (var commandWorkflow in workflows)
        {
            if (!commandWorkflow.Accepts(input)) continue;

            commandWorkflow.Inject(_scopeFactory, _config, loggerFactory);
            return commandWorkflow;
        }

        return null;
    }
}