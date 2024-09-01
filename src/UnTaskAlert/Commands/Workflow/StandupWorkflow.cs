using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using UnTaskAlert.Models;

namespace UnTaskAlert.Commands.Workflow
{
    public class StandupWorkflow : CommandWorkflow
    {
        protected override async Task<WorkflowResult> PerformStep(string input, Subscriber subscriber, long chatId)
        {
            var startDate = DateTime.Now;

            await ReportingService.CreateStandupReport(subscriber,
                Config.AzureDevOpsAddress,
                Config.AzureDevOpsAccessToken,
                Logger);

            return WorkflowResult.Finished;
        }

        protected override void InjectDependencies(IServiceScopeFactory serviceScopeFactory)
        {
            // no-op
        }

        protected override bool DoesAccept(string input) => input.StartsWith("/standup", StringComparison.OrdinalIgnoreCase);
    }
}
