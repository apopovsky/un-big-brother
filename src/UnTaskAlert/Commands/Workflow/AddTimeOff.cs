using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using UnTaskAlert.Models;

namespace UnTaskAlert.Commands.Workflow;

public class AddTimeOff : CommandWorkflow
{
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
            await Notifier.Respond(chatId, "Please provide a valid date (dd.MM.yyyy, yyyyMMdd, dd/MM/yyyy, or dd-MM-yyyy format) or leave empty to use the current date.");
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
                await Notifier.Respond(chatId, $"{timeOffInHours} hours added as time off on {date.Value.Date}");
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
                await Notifier.Respond(chatId, $"{timeOffInHours} hours added as time off on {date.Value.Date}");
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
                await Notifier.Respond(chatId, $"Time off removed from {date.Value.Date}");
            }
        }

        return WorkflowResult.Finished;
    }

    private static DateTime? ParseDate(string[] inputParts) =>
        inputParts.Length >= 3
            ? DateTime.TryParseExact(
                inputParts[2],
                ["dd.MM.yyyy", "yyyyMMdd", "dd/MM/yyyy", "dd-MM-yyyy"], CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var date)
                ? date
                : null
            : DateTime.Today;
}