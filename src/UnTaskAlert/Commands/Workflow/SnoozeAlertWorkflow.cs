using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UnTaskAlert.Models;

namespace UnTaskAlert.Commands.Workflow;

public class SnoozeAlertWorkflow : CommandWorkflow
{
    protected override async Task<WorkflowResult> PerformStep(string input, Subscriber subscriber, long chatId)
    {
        var inputParts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        int minutes;
        if (inputParts.Length > 1)
        {
            var parsed = int.TryParse(inputParts[1], out minutes);
            if (!parsed || minutes<=0)
            {
                await Notifier.Respond(chatId,"Please provide a valid number of minutes to snooze alerts");
                return WorkflowResult.Continue;
            }
        }
        else
        {
            minutes = 30;
        }

        subscriber.SnoozeAlertsUntil = DateTime.UtcNow.AddMinutes(minutes);
        await Notifier.Respond(chatId, $"You won't receive any alerts for the next {minutes} minutes.");

        return WorkflowResult.Finished;
    }

    protected override void InjectDependencies(IServiceScopeFactory serviceScopeFactory)
    {
        // no-op
    }

    protected override bool DoesAccept(string input) => input.StartsWith("/snooze", StringComparison.OrdinalIgnoreCase);
}