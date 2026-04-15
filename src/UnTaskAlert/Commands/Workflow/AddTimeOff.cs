using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using UnTaskAlert.Models;

namespace UnTaskAlert.Commands.Workflow;

public class AddTimeOff : CommandWorkflow
{
    private static readonly string[] validFormats = DateUtils.AcceptedDateInputFormats;

    protected override void InjectDependencies(IServiceScopeFactory serviceScopeFactory)
    {
        // no-op
    }

    protected override bool DoesAccept(string input) => input.StartsWith("/addtimeoff", StringComparison.OrdinalIgnoreCase);

    protected override async Task<WorkflowResult> PerformStep(string input, Subscriber subscriber, long chatId)
    {
        var inputParts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (!TryParseInputArguments(inputParts, out var date, out var timeOffInHours, out var validationError))
        {
            await Notifier.Respond(chatId, validationError);
            return WorkflowResult.Finished;
        }

        subscriber.TimeOff ??= [];

        var targetDay = subscriber.TimeOff.FirstOrDefault(x => x.Date.Date.Equals(date.Date));
        if (targetDay == null)
        {
            if (timeOffInHours >= 0)
            {
                subscriber.TimeOff.Add(new TimeOff
                {
                    Date = date.Date,
                    HoursOff = timeOffInHours,
                });
                await Notifier.Respond(chatId, $"{timeOffInHours} hours added as time off on {date.ToShortDateString()}");
            }
            else
            {
                var message = $"No time off found for {date:dd.MM.yyyy}. Nothing to do.";
                await Notifier.Respond(chatId, message);
            }
        }
        else
        {
            if (timeOffInHours >= 0)
            {
                targetDay.HoursOff += timeOffInHours;
                await Notifier.Respond(chatId, $"{timeOffInHours} hours added as time off on {date.ToShortDateString()}");
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
                await Notifier.Respond(chatId, $"Time off removed from {date.ToShortDateString()}");
            }
        }

        return WorkflowResult.Finished;
    }

    private static bool TryParseInputArguments(string[] inputParts, out DateTime date, out int timeOffInHours, out string validationError)
    {
        date = DateTime.Today;
        timeOffInHours = 0;
        validationError = string.Empty;

        if (inputParts.Length < 2 || inputParts.Length > 3)
        {
            validationError = BuildUsageMessage();
            return false;
        }

        var args = inputParts.Skip(1).ToArray();

        if (args.Length == 1)
        {
            if (int.TryParse(args[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out timeOffInHours))
            {
                return true;
            }

            validationError = TryParseDate(args[0], out _)
                ? "Please provide hours as well. Example: /addtimeoff 8 2026-03-20"
                : BuildUsageMessage();

            return false;
        }

        if (TryParseDate(args[0], out var firstDate)
            && int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var secondHours))
        {
            date = firstDate;
            timeOffInHours = secondHours;
            return true;
        }

        if (int.TryParse(args[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var firstHours)
            && TryParseDate(args[1], out var secondDate))
        {
            date = secondDate;
            timeOffInHours = firstHours;
            return true;
        }

        validationError = BuildUsageMessage();
        return false;
    }

    private static bool TryParseDate(string input, out DateTime date)
    {
        return DateTime.TryParseExact(
            input,
            validFormats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out date);
    }

    private static string BuildUsageMessage()
    {
        var formats = string.Join(Environment.NewLine, validFormats.Select(x => $"- {x}"));
        return "Please provide time off as '/addtimeoff <hours> [date]' or '/addtimeoff <date> <hours>'.\n"
             + "Examples: /addtimeoff 8, /addtimeoff 8 2026-03-20, /addtimeoff 2026-03-20 8\n"
             + "Accepted date formats:\n"
             + formats;
    }
}