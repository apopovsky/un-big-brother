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
    IServiceScopeFactory scopeFactory)
    : ICommandProcessor
{
    private readonly INotifier _notifier = Arg.NotNull(notifier, nameof(notifier));
    private readonly Config _config = Arg.NotNull(options.Value, nameof(options));
    private readonly IDbAccessor _dbAccessor = Arg.NotNull(dbAccessor, nameof(dbAccessor));
    private readonly IPinGenerator _pinGenerator = Arg.NotNull(pinGenerator, nameof(pinGenerator));
    private readonly IServiceScopeFactory _scopeFactory = Arg.NotNull(scopeFactory, nameof(scopeFactory));

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

        var chatId = update.Message.Chat.Id;

        log.LogInformation("Processing the command: {MessageText}", update.Message.Text);
        await _notifier.Typing(chatId, cancellationToken);

        var subscriber = await _dbAccessor.GetSubscriberById(update.Message.Chat.Id.ToString(), log);

        if (subscriber == null)
        {
            log.LogInformation("Process: Subscriber is 'null'");
            await _notifier.Respond(update.Message.Chat.Id, "Nothing to see here!");
            return;
        }
        else
        {
            log.LogInformation("TelegramId: {TelegramId}\n" +
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
            new PullRequestsWorkflow(),
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
        
        var commandWorkflow = ProcessInput(input, log, workflows) ?? throw new InvalidOperationException($"The bot is lost and doesn't know what to do. chatId '{subscriber.TelegramId}'.");

        var workflowResult = await commandWorkflow.Step(input, subscriber, update.Message.Chat.Id, cancellationToken);
        
        subscriber.ActiveWorkflow = workflowResult == WorkflowResult.Finished ? null : commandWorkflow;
        await _dbAccessor.AddOrUpdateSubscriber(subscriber, cancellationToken);
    }

    private async Task<Subscriber> NewUserFlow(long chatId, ILogger log, CancellationToken cancellationToken)
    {
        log.LogInformation("NewUserFlow() is executed for chatId '{ChatId}'", chatId);
        await Task.Delay(PauseBeforeAnswer, cancellationToken);
        var workflow = new AccountWorkflow();
        workflow.Inject(_scopeFactory, _config, log);

        var subscriber = new Subscriber
        {
            Email = string.Empty,
            TelegramId = chatId.ToString(),
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

    private CommandWorkflow ProcessInput(string input, ILogger log, params CommandWorkflow[] workflows)
    {
        foreach (var commandWorkflow in workflows)
        {
            if (!commandWorkflow.Accepts(input)) continue;

            commandWorkflow.Inject(_scopeFactory, _config, log);
            return commandWorkflow;
        }

        return null;
    }
}