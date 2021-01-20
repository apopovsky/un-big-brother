using System;
using System.Collections.Generic;
using System.Globalization;
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
            var date = DateTime.UtcNow.Date;

            if (inputParts.Length == 2 || inputParts.Length == 3)
            {
                var parsed = int.TryParse(inputParts[1], out timeOffInHours);
                if (!parsed)
                {
                    await ShowError(chatId, "Please provide a valid number of hours off (ex. /addtimeoff 8");
                    return WorkflowResult.Continue;
                }

                if (inputParts.Length == 3)
                {
                    parsed = DateTime.TryParseExact(inputParts[2], "dd.MM.yyyy", null, DateTimeStyles.None, out date);
                    if (!parsed)
                    {
                        await ShowError(chatId, "Please provide a valid date or leave empty to use current");
                        return WorkflowResult.Continue;
                    }
                }
            }
            else
            {
                await ShowError(chatId, "Please provide a valid number of hours off (ex. /addtimeoff 8) and optionally the date in dd.MM.yyyy format.");
                return WorkflowResult.Continue;
            }

            if (subscriber.TimeOff == null)
            {
                subscriber.TimeOff = new List<TimeOff>();
            }

            subscriber.TimeOff.Add(new TimeOff
            {
                Date = date,
                HoursOff = timeOffInHours
            });
            await Notifier.Respond(chatId, $"{timeOffInHours} hours added as time off on {date}");

            return WorkflowResult.Finished;
        }

        private async Task ShowError(long chatId, string message)
        {
            await Notifier.Respond(chatId, message);
        }
    }
}
