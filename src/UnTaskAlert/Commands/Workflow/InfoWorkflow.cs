using System;
using System.Threading.Tasks;
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

        protected override bool DoesAccept(string input)
        {
            return input.StartsWith("/standup", StringComparison.OrdinalIgnoreCase);
        }
    }
}
