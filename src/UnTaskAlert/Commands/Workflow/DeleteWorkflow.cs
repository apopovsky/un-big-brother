using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UnTaskAlert.Models;

namespace UnTaskAlert.Commands.Workflow;

public class DeleteWorkflow : CommandWorkflow
{
    private DbAccessor _dbAccessor;

    private enum Steps
    {
        Confirm = 0,
    }

    protected override async Task<WorkflowResult> PerformStep(string input, Subscriber subscriber, long chatId)
    {
        if (CurrentStep == (int)Steps.Confirm)
        {
            await Notifier.Respond(chatId, "Are you sure? (Y/N)");
            return WorkflowResult.Continue;
        }

        if (!input.Equals("y", StringComparison.OrdinalIgnoreCase) &&
            !input.Equals("yes", StringComparison.OrdinalIgnoreCase)) return WorkflowResult.Finished;
            
        Logger.LogInformation("Deleting subscriber '{id}'", subscriber.TelegramId);
        await _dbAccessor.DeleteIfExists(subscriber);

        return WorkflowResult.Finished;
    }

    protected override void InjectDependencies(IServiceScopeFactory serviceScopeFactory)
    {
        var serviceProvider = serviceScopeFactory.CreateScope().ServiceProvider;
        var options = serviceProvider.GetService<IOptions<Config>>();
        var loggerFactory = serviceProvider.GetService<ILoggerFactory>();
        _dbAccessor = new DbAccessor(serviceScopeFactory, options, loggerFactory);
    }

    protected override bool DoesAccept(string input) => input.StartsWith("/delete", StringComparison.OrdinalIgnoreCase);
}