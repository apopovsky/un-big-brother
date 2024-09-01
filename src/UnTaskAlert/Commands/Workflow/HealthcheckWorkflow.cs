using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using UnTaskAlert.Models;

namespace UnTaskAlert.Commands.Workflow
{
    public class HealthcheckWorkflow : CommandWorkflow
    {
        private const string HealthcheckCommand = "/healthcheck";

        protected override async Task<WorkflowResult> PerformStep(string input, Subscriber subscriber, long chatId)
        {
            double threshold = 0;
            if (input.Length > HealthcheckCommand.Length)
            {
                double.TryParse(input.Substring(HealthcheckCommand.Length), out threshold);
            }

            await ReportingService.CreateHealthCheckReport(subscriber,
                Config.AzureDevOpsAddress,
                Config.AzureDevOpsAccessToken,
                DateUtils.StartOfMonth(),
                threshold,
                Logger);

            return WorkflowResult.Finished;
        }

        protected override void InjectDependencies(IServiceScopeFactory serviceScopeFactory)
        {
            // no-op
        }

        protected override bool DoesAccept(string input) => input.StartsWith(HealthcheckCommand, StringComparison.OrdinalIgnoreCase);
    }
}
