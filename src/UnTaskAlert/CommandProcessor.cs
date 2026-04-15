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

    private static string MaskEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return string.Empty;
        }

        var at = email.IndexOf('@');
        if (at <= 1)
        {
            return "***";
        }

        var domain = at >= 0 ? email[at..] : string.Empty;
        return $"{email[0]}***{domain}";
    }

    private static void ClearActiveWorkflow(Subscriber subscriber)
    {
        subscriber.ActiveWorkflow = null;
        subscriber.ActiveWorkflowName = null;
        subscriber.ActiveWorkflowStep = null;
        subscriber.ActiveWorkflowExpirationUtc = null;
        subscriber.ActiveWorkflowData = null;
    }

    private static void PersistActiveWorkflowState(Subscriber subscriber, CommandWorkflow workflow)
    {
        subscriber.ActiveWorkflow = workflow;
        subscriber.ActiveWorkflowName = workflow.GetType().FullName;
        subscriber.ActiveWorkflowStep = workflow.CurrentStep;
        subscriber.ActiveWorkflowExpirationUtc = workflow.Expiration;
        subscriber.ActiveWorkflowData = workflow.Data;
    }


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

        log.LogInformation("Processing message. chatId={ChatId}", chatId);
        await _notifier.Typing(chatId, cancellationToken);

        var subscriber = await _dbAccessor.GetSubscriberById(update.Message.Chat.Id.ToString(), log);

        if (subscriber == null)
        {
            subscriber = await NewUserFlow(chatId, log, cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(subscriber.ActiveWorkflowName) && subscriber.ActiveWorkflow == null)
        {
            var restored = TryRestoreWorkflow(subscriber.ActiveWorkflowName);
            if (restored != null)
            {
                restored.CurrentStep = subscriber.ActiveWorkflowStep ?? 0;
                restored.Data = subscriber.ActiveWorkflowData;
                if (subscriber.ActiveWorkflowExpirationUtc.HasValue)
                {
                    restored.Expiration = subscriber.ActiveWorkflowExpirationUtc.Value;
                }

                if (restored.IsExpired)
                {
                    ClearActiveWorkflow(subscriber);
                }
                else
                {
                    restored.Inject(_scopeFactory, _config, log);
                    subscriber.ActiveWorkflow = restored;
                }
            }
            else
            {
                ClearActiveWorkflow(subscriber);
            }
        }

        log.LogInformation(
            "Subscriber state: TelegramId={TelegramId}, IsVerified={IsVerified}, VerificationAttempts={VerificationAttempts}, Email={Email}",
            subscriber.TelegramId,
            subscriber.IsVerified,
            subscriber.VerificationAttempts,
            MaskEmail(subscriber.Email));

        if (input.StartsWith("/abort", StringComparison.OrdinalIgnoreCase))
        {
            if (subscriber.ActiveWorkflow != null)
            {
                ClearActiveWorkflow(subscriber);
                await _dbAccessor.AddOrUpdateSubscriber(subscriber, cancellationToken);
                await _notifier.Respond(chatId, "Command aborted");
            }
            else
            {
                await _notifier.Respond(chatId, "No active command");
            }

            return;
        }

        if (subscriber.ActiveWorkflow is { IsExpired: false })
        {
            var result = await subscriber.ActiveWorkflow.Step(input, subscriber, update.Message.Chat.Id, cancellationToken);
            if (result == WorkflowResult.Finished)
            {
                ClearActiveWorkflow(subscriber);
            }
            else
            {
                PersistActiveWorkflowState(subscriber, subscriber.ActiveWorkflow);
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
            new PullRequestsApprovalWorkflow(),
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

        if (workflowResult == WorkflowResult.Finished)
        {
            ClearActiveWorkflow(subscriber);
        }
        else
        {
            PersistActiveWorkflowState(subscriber, commandWorkflow);
        }
        await _dbAccessor.AddOrUpdateSubscriber(subscriber, cancellationToken);
    }

    private static CommandWorkflow TryRestoreWorkflow(string workflowName)
    {
        if (string.IsNullOrWhiteSpace(workflowName))
        {
            return null;
        }

        return workflowName switch
        {
            "UnTaskAlert.Commands.Workflow.SnoozeAlertWorkflow" => new SnoozeAlertWorkflow(),
            "UnTaskAlert.Commands.Workflow.SetSettingsWorkflow" => new SetSettingsWorkflow(),
            "UnTaskAlert.Commands.Workflow.ActiveWorkflow" => new ActiveWorkflow(),
            "UnTaskAlert.Commands.Workflow.PullRequestsWorkflow" => new PullRequestsWorkflow(),
            "UnTaskAlert.Commands.Workflow.PullRequestsApprovalWorkflow" => new PullRequestsApprovalWorkflow(),
            "UnTaskAlert.Commands.Workflow.StandupWorkflow" => new StandupWorkflow(),
            "UnTaskAlert.Commands.Workflow.DayWorkflow" => new DayWorkflow(),
            "UnTaskAlert.Commands.Workflow.WeekWorkflow" => new WeekWorkflow(),
            "UnTaskAlert.Commands.Workflow.MonthWorkflow" => new MonthWorkflow(),
            "UnTaskAlert.Commands.Workflow.YearWorkflow" => new YearWorkflow(),
            "UnTaskAlert.Commands.Workflow.HealthcheckWorkflow" => new HealthcheckWorkflow(),
            "UnTaskAlert.Commands.Workflow.InfoWorkflow" => new InfoWorkflow(),
            "UnTaskAlert.Commands.Workflow.AddTimeOff" => new AddTimeOff(),
            "UnTaskAlert.Commands.Workflow.DeleteWorkflow" => new DeleteWorkflow(),
            "UnTaskAlert.Commands.Workflow.AccountWorkflow" => new AccountWorkflow(),
            "UnTaskAlert.Commands.Workflow.StoryInfoWorkflow" => new StoryInfoWorkflow(),
            _ => null,
        };
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

        PersistActiveWorkflowState(subscriber, workflow);

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