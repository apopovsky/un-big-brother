using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using UnTaskAlert.Common;
using UnTaskAlert.Models;

namespace UnTaskAlert.Commands.Workflow;

[Serializable]
public abstract class CommandWorkflow
{
    private bool _isInitialized;

    private const int PauseBeforeAnswer = 2000;

    protected ILogger<CommandWorkflow> Logger { get; set; }
    protected INotifier Notifier { get; set; }
    protected IReportingService ReportingService { get; set; }
    protected Config Config { get; set; }

    [JsonIgnore]
    public virtual bool IsVerificationRequired => true;
    [JsonIgnore]
    public bool IsExpired => DateTime.UtcNow > Expiration;
    public DateTime Expiration { get; set; } = DateTime.UtcNow.AddMinutes(5);
    public int CurrentStep { get; set; }

    public void Inject(IServiceScopeFactory serviceScopeFactory, Config config, ILoggerFactory loggerFactory)
    {
        Logger = loggerFactory.CreateLogger<CommandWorkflow>();
        Config = Arg.NotNull(config, nameof(config));
        var serviceScope = serviceScopeFactory.CreateScope();
        Notifier = Arg.NotNull(serviceScope.ServiceProvider.GetService<INotifier>(), $"Could not resolve '{nameof(INotifier)}'");
        ReportingService = Arg.NotNull(serviceScope.ServiceProvider.GetService<IReportingService>(), $"Could not resolve '{nameof(IReportingService)}'");

        InjectDependencies(serviceScopeFactory);

        _isInitialized = true;
    }

    protected abstract void InjectDependencies(IServiceScopeFactory serviceScopeFactory);

    public async Task<WorkflowResult> Step(string input, Subscriber subscriber, long chatId, CancellationToken cancellationToken)
    {
        Logger.LogInformation("Executing workflow {WorkflowType} for subscriber '{TelegramId}'", this.GetType(), subscriber.TelegramId);

        if (!_isInitialized)
        {
            throw new InvalidOperationException("Workflow is not initialized. Call 'Inject' first.");
        }

        if (IsVerificationRequired && !subscriber.IsVerified)
        {
            Logger.LogInformation("Command '{Command}' is available only for verified users", input);
            await Notifier.Respond(chatId, "Verification is required");
            return WorkflowResult.Finished;
        }

        if (!IsVerificationRequired && !subscriber.IsVerified)
        {
            // making a pause for security reasons
            Logger.LogInformation("Pause responding for {PauseDuration} ms", PauseBeforeAnswer);
            await Task.Delay(PauseBeforeAnswer, cancellationToken);
        }

        if (input.Equals("/cancel", StringComparison.OrdinalIgnoreCase))
        {
            await Notifier.Respond(chatId, "Command cancelled");
            return WorkflowResult.Finished;
        }

        try
        {
            await Notifier.Typing(chatId.ToString(), cancellationToken);
            return await PerformStep(input, subscriber, chatId);
        }
        catch (Exception e)
        {
            Logger.LogError(e, "Something bad happened to the bot :(");
            await Notifier.Respond(chatId, "Something bad happened to the bot :(");
            throw;
        }
    }

    public bool Accepts(string input)
    {
        if (input.Equals("/exit", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return DoesAccept(input);
    }

    public string Data { get; set; }

    protected abstract bool DoesAccept(string input);
    protected abstract Task<WorkflowResult> PerformStep(string input, Subscriber subscriber, long chatId);
}

public enum WorkflowResult
{
    Continue,
    Finished,
}