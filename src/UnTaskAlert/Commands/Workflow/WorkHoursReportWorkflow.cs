using System;
using System.Globalization;
using System.Threading.Tasks;
using UnTaskAlert.Models;

namespace UnTaskAlert.Commands.Workflow
{
    public abstract class WorkHoursReportWorkflow : CommandWorkflow
    {
        protected abstract string Command { get; set; }
        protected abstract DateTime StartDate { get; set; }
        protected virtual DateTime? EndDate { get; set; }

        protected override void InjectDependencies(IServiceProvider serviceProvider)
        {
            // no-op
        }

        protected override async Task<WorkflowResult> PerformStep(string input, Subscriber subscriber, long chatId)
        {
            var strings = input.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (strings.Length > 1 && DateTime.TryParseExact(strings[1],
                    new[] { "yyyy.MM.dd", "yyyyMMdd", "dd.MM.yyyy", "dd/MM/yyyy" },
                    CultureInfo.CurrentCulture,
                    DateTimeStyles.None, out var startDate))
            {
                StartDate = startDate;
            }
            else if (strings.Length > 1 && TryParseMonth(strings[1], out var month))
            {
                StartDate = GetStartOfMonth(month);
                EndDate = GetEndOfMonth(month);
            }

            if (strings.Length > 2 && DateTime.TryParseExact(strings[2],
                    new[] { "yyyy.MM.dd", "yyyyMMdd", "dd.MM.yyyy", "dd/MM/yyyy" },
                    CultureInfo.CurrentCulture,
                    DateTimeStyles.None, out var endDate))
            {
                EndDate = endDate;
            }
            else if (strings.Length > 2 && TryParseMonth(strings[2], out var month))
            {
                EndDate = GetEndOfMonth(month);
            }

            await ReportingService.CreateWorkHoursReport(subscriber,
                Config.AzureDevOpsAddress,
                Config.AzureDevOpsAccessToken,
                StartDate,
                Logger, EndDate);

            return WorkflowResult.Finished;
        }

        protected override bool DoesAccept(string input)
        {
            return input.StartsWith(Command, StringComparison.OrdinalIgnoreCase);
        }

        private bool TryParseMonth(string input, out int month)
        {
            if (int.TryParse(input, out month))
            {
                if (month >= 1 && month <= 12)
                {
                    return true;
                }
            }
            else
            {
                var culture = CultureInfo.GetCultureInfo("en-US");
                var monthNames = culture.DateTimeFormat.MonthNames;
                for (int i = 0; i < monthNames.Length; i++)
                {
                    if (string.Equals(monthNames[i], input, StringComparison.OrdinalIgnoreCase))
                    {
                        month = i + 1;
                        return true;
                    }
                }

                culture = CultureInfo.GetCultureInfo("es-ES");
                monthNames = culture.DateTimeFormat.MonthNames;
                for (int i = 0; i < monthNames.Length; i++)
                {
                    if (string.Equals(monthNames[i], input, StringComparison.OrdinalIgnoreCase))
                    {
                        month = i + 1;
                        return true;
                    }
                }
            }

            month = 0;
            return false;
        }

        private DateTime GetStartOfMonth(int month)
        {
            var today = DateTime.Today;
            var year = today.Year;
            if (month > today.Month)
            {
                year--;
            }
            return new DateTime(year, month, 1);
        }

        private DateTime GetEndOfMonth(int month)
        {
            var today = DateTime.Today;
            var year = today.Year;
            if (month > today.Month)
            {
                year--;
            }
            var lastDayOfMonth = DateTime.DaysInMonth(year, month);
            return new DateTime(year, month, lastDayOfMonth);
        }
    }

    public class DayWorkflow : WorkHoursReportWorkflow
    {
        protected override string Command { get; set; } = "/day";
        protected override DateTime StartDate { get; set; } = DateTime.Today;
    }

    public class WeekWorkflow : WorkHoursReportWorkflow
    {
        protected override string Command { get; set; } = "/week";
        protected override DateTime StartDate { get; set; } = DateUtils.StartOfWeek();
    }

    public class MonthWorkflow : WorkHoursReportWorkflow
    {
        protected override string Command { get; set; } = "/month";
        protected override DateTime StartDate { get; set; } = DateUtils.StartOfMonth();
        
    }

    public class YearWorkflow : WorkHoursReportWorkflow
    {
        protected override string Command { get; set; } = "/year";
        protected override DateTime StartDate { get; set; } = new(DateTime.Today.Year, 1, 1);
    }
}
