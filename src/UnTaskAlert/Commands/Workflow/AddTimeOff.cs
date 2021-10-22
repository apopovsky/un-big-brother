using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
                    await Notifier.Respond(chatId, $"Please provide a valid number of hours off (ex. /addtimeoff 8 or /addtimeoff 8 01.05.{DateTime.Today.Year}]");
                    return WorkflowResult.Continue;
                }

                if (inputParts.Length == 3)
                {
                    var dateString = inputParts[2].Replace('/', '.').Replace('-', '.');
                    parsed = DateTime.TryParseExact(dateString, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
                    if (!parsed)
                    {
                        await Notifier.Respond(chatId, "Please provide a valid date (dd.MM.yyyy format) or leave empty to use current");
                        return WorkflowResult.Continue;
                    }
                }
            }
            else
            {
                await Notifier.Respond(chatId, "Please provide a valid number of hours off (ex. /addtimeoff 8) and optionally the date in dd.MM.yyyy format.");
                return WorkflowResult.Continue;
            }

            subscriber.TimeOff ??= new List<TimeOff>();

            if (timeOffInHours >= 0)
            {
                var targetDay = subscriber.TimeOff.FirstOrDefault(x => x.Date.Date.Equals(date));
                if (targetDay == null)
                {
                    subscriber.TimeOff.Add(new TimeOff
                    {
                        Date = date,
                        HoursOff = timeOffInHours
                    });
                }
                else
                {
                    targetDay.HoursOff += timeOffInHours;
                }
                
                await Notifier.Respond(chatId, $"{timeOffInHours} hours added as time off on {date}");
            }
            else
            {
                var targetDay = subscriber.TimeOff.FirstOrDefault(x => x.Date.Date.Equals(date));
                if (targetDay == null)
                {
                    string message = $"No time off found for {date:dd.MM.yyyy}. Nothing to do.";
                    await Notifier.Respond(chatId, message);
                    return WorkflowResult.Continue;
                }

                if (targetDay.HoursOff <= Math.Abs(timeOffInHours))
                {
                    subscriber.TimeOff.Remove(targetDay);
                }
                else
                {
                    targetDay.HoursOff += timeOffInHours;
                }

                await Notifier.Respond(chatId, $"Time off removed from {date}");
            }

            return WorkflowResult.Finished;
        }
    }
}
