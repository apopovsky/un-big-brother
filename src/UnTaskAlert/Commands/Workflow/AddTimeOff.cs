using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnTaskAlert.Models;

namespace UnTaskAlert.Commands.Workflow
{
    public class AddTimeOff : CommandWorkflow
    {
        protected override void InjectDependencies(IServiceProvider serviceProvider)
        {
            // no-op
        }

        protected override bool DoesAccept(string input)
        {
            return input.StartsWith("/addtimeoff", StringComparison.OrdinalIgnoreCase);
        }

        protected override async Task<WorkflowResult> PerformStep(string input, Subscriber subscriber, long chatId)
        {
            var inputParts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            int timeOffInHours;

            if (inputParts.Length == 2)
            {
                var parsed = int.TryParse(inputParts[1], out timeOffInHours);
                if (!parsed)
                {
                    await ShowError(chatId);
                    return WorkflowResult.Continue;
                }
            }
            else
            {
                await ShowError(chatId);
                return WorkflowResult.Continue;
            }

            if (subscriber.TimeOff == null)
            {
                subscriber.TimeOff = new List<TimeOff>();
            }

            var now = DateTime.UtcNow;
            subscriber.TimeOff.Add(new TimeOff
            {
                Date = now,
                HoursOff = timeOffInHours
            });
            await Notifier.Respond(chatId, $"{timeOffInHours} hours added as time off on {now}");

            return WorkflowResult.Finished;
        }

        private async Task ShowError(long chatId)
        {
            await Notifier.Respond(chatId, "Please provide a valid number of hours off (ex. /addtimeoff 8");
        }
    }
}
