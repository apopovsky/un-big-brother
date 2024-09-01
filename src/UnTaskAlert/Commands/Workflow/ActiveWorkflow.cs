using Microsoft.Extensions.DependencyInjection;
using UnTaskAlert.Models;

namespace UnTaskAlert.Commands.Workflow
{
    public class ActiveWorkflow : CommandWorkflow
    {
        protected override async Task<WorkflowResult> PerformStep(string input, Subscriber subscriber, long chatId)
        {
            var startDate = DateTime.Now;

            await ReportingService.ActiveTasksReport(subscriber,
                Config.AzureDevOpsAddress,
                Config.AzureDevOpsAccessToken,
                startDate,
                Logger);

            return WorkflowResult.Finished;
        }

        protected override void InjectDependencies(IServiceScopeFactory serviceScopeFactory)
        {
            // no-op
        }

        protected override bool DoesAccept(string input) => input.StartsWith("/active", StringComparison.OrdinalIgnoreCase);
    }
}
