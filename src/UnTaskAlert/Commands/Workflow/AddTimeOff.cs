using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using UnTaskAlert.Models;

namespace UnTaskAlert.Commands.Workflow;

public class AddTimeOff : CommandWorkflow
{
    private static readonly string[] validFormats = ["dd.MM.yyyy", "yyyyMMdd", "dd/MM/yyyy", "dd-MM-yyyy", "yyyy-MM-dd", "yyyyMMdd"];

    protected override void InjectDependencies(IServiceScopeFactory serviceScopeFactory)
    {
        // no-op
    }

    protected override bool DoesAccept(string input) => input.StartsWith("/addtimeoff", StringComparison.OrdinalIgnoreCase);

    protected override async Task<WorkflowResult> PerformStep(string input, Subscriber subscriber, long chatId)
    {
        var inputParts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var date = ParseDate(inputParts);

        if (date == null)
        {
            await Notifier.Respond(chatId, "Please provide a valid date or leave empty to use the current date.\n" +
                                           "Accepted formats: \n" +
                                           validFormats.Select(x => $"\u25cf {x}\n")
                                           );
            return WorkflowResult.Finished;
        }

        if (inputParts.Length < 2 || !int.TryParse(inputParts[1], out var timeOffInHours))
        {
            await Notifier.Respond(chatId, "Please provide a valid number of hours off (ex. /addtimeoff 8) and optionally the date in dd.MM.yyyy, yyyyMMdd, dd/MM/yyyy, or dd-MM-yyyy format.");
            return WorkflowResult.Finished;
        }

        subscriber.TimeOff ??= [];

        var targetDay = subscriber.TimeOff.FirstOrDefault(x => x.Date.Date.Equals(date.Value.Date));
        if (targetDay == null)
        {
            if (timeOffInHours >= 0)
            {
                subscriber.TimeOff.Add(new TimeOff
                {
                    Date = date.Value.Date,
                    HoursOff = timeOffInHours,
                });
                await Notifier.Respond(chatId, $"{timeOffInHours} hours added as time off on {date.Value.ToShortDateString()}");
            }
            else
            {
                var message = $"No time off found for {date.Value:dd.MM.yyyy}. Nothing to do.";
                await Notifier.Respond(chatId, message);
            }
        }
        else
        {
            if (timeOffInHours >= 0)
            {
                targetDay.HoursOff += timeOffInHours;
                await Notifier.Respond(chatId, $"{timeOffInHours} hours added as time off on {date.Value.ToShortDateString()}");
            }
            else
            {
                if (targetDay.HoursOff <= Math.Abs(timeOffInHours))
                {
                    subscriber.TimeOff.Remove(targetDay);
                }
                else
                {
                    targetDay.HoursOff += timeOffInHours;
                }
                await Notifier.Respond(chatId, $"Time off removed from {date.Value.ToShortDateString()}");
            }
        }

        return WorkflowResult.Finished;
    }

    private static DateTime? ParseDate(string[] inputParts)
    {
        DateTime? parsedDate = DateTime.TryParseExact(
                        inputParts[2],
                        validFormats, CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out var date)
                        ? date
                        : null;

        return inputParts.Length >= 3
                    ? parsedDate
                    : DateTime.Today;
    }
}