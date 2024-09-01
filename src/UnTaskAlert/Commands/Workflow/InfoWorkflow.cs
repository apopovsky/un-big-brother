using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using UnTaskAlert.Models;

namespace UnTaskAlert.Commands.Workflow
{
    public class InfoWorkflow : CommandWorkflow
    {
        protected override async Task<WorkflowResult> PerformStep(string input, Subscriber subscriber, long chatId)
        {
            await Notifier.AccountInfo(subscriber);

            return WorkflowResult.Finished;
        }

        protected override void InjectDependencies(IServiceScopeFactory serviceScopeFactory)
        {
            // no-op
        }

        protected override bool DoesAccept(string input) => input.StartsWith("/info", StringComparison.OrdinalIgnoreCase);
    }
}
