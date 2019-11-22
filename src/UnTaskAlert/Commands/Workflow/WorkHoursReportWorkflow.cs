using System;
using System.Threading.Tasks;
using UnTaskAlert.Models;

namespace UnTaskAlert.Commands.Workflow
{
    public abstract class WorkHoursReportWorkflow : CommandWorkflow
    {
        protected abstract string Command { get; set; }
        protected abstract DateTime StartDate { get; set; }

        protected override void InjectDependencies(IServiceProvider serviceProvider)
        {
            // no-op
        }

        protected override async Task<WorkflowResult> PerformStep(string input, Subscriber subscriber, long chatId)
        {
            await ReportingService.CreateWorkHoursReport(subscriber,
                Config.AzureDevOpsAddress,
                Config.AzureDevOpsAccessToken,
                StartDate,
                Logger);

            return WorkflowResult.Finished;
        }

        protected override bool DoesAccept(string input)
        {
            return input.StartsWith(Command, StringComparison.OrdinalIgnoreCase);
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
        protected override DateTime StartDate { get; set; } = new DateTime(DateTime.Today.Year, 1, 1);
    }
}
