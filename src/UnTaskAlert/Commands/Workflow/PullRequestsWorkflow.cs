using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using UnTaskAlert.Models;

namespace UnTaskAlert.Commands.Workflow;

public class PullRequestsWorkflow : CommandWorkflow
{
    protected override async Task<WorkflowResult> PerformStep(string input, Subscriber subscriber, long chatId)
    {
        if (subscriber.AzureDevOpsProjects == null || subscriber.AzureDevOpsProjects.Count == 0)
        {
            await Notifier.Respond(chatId,
                "Please set your Azure DevOps project(s) first. Use /setsettings and set Projects=MeetingsTeam (or Projects=ProjA,ProjB).\n" +
                "Example: Projects=MeetingsTeam");
            return WorkflowResult.Finished;
        }

        try
        {
            await ReportingService.ActivePullRequestsReport(
                subscriber,
                Config.AzureDevOpsAddress,
                Config.AzureDevOpsAccessToken,
                Logger);
        }
        catch (InvalidOperationException ex)
        {
            Logger.LogError(ex, "Failed to query pull requests for {Email}", subscriber.Email);
            await Notifier.Respond(chatId, ex.Message);
        }

        return WorkflowResult.Finished;
    }

    protected override void InjectDependencies(IServiceScopeFactory serviceScopeFactory)
    {
        // no-op
    }

    protected override bool DoesAccept(string input) => input.StartsWith("/pr", StringComparison.OrdinalIgnoreCase);
}