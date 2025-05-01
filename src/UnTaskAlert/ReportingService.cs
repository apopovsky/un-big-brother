using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using UnTaskAlert.Common;
using UnTaskAlert.Models;

namespace UnTaskAlert;

public class ReportingService(INotifier notifier, IBacklogAccessor backlogAccessor) : IReportingService
{
    public const int HoursPerDay = 8;

    private readonly INotifier _notifier = Arg.NotNull(notifier, nameof(notifier));
    private readonly IBacklogAccessor _backlogAccessor = Arg.NotNull(backlogAccessor, nameof(backlogAccessor));

    public async Task CreateWorkHoursReport(Subscriber subscriber, string url, string token, DateTime startDate,
        ILogger log, DateTime? endDate)
    {
        var report = await GetTimeReport(subscriber, url, token, startDate, log, endDate);

        log.LogInformation("Query Result: totalActive:'{TotalActive}', totalEstimated:'{TotalEstimated}', totalCompleted:'{TotalCompleted}', expected: '{Expected}'",
            report.TotalActive, report.TotalEstimated, report.TotalCompleted, report.Expected);

        await SendReport(subscriber, report, log);
    }

    public async Task<ActiveTasksInfo> ActiveTasksReport(Subscriber subscriber, string url, string token, DateTime startDate, ILogger log)
    {
        var orgUrl = new Uri(url);

        var connection = new VssConnection(orgUrl, new VssBasicCredential(string.Empty, token));
        var activeTaskInfo = await _backlogAccessor.GetActiveWorkItems(connection, subscriber.Email, log);
        foreach (var taskInfo in activeTaskInfo.TasksInfo)
        {
            taskInfo.ActiveTime = (await backlogAccessor.GetWorkItemActiveTime(connection, taskInfo.Id)).TotalHours;
            var parent = (await backlogAccessor.GetParentUserStory(connection, taskInfo.Id));
            Console.WriteLine($"Parent: {parent.Fields["System.Title"].ToString()}");
        }

        await _notifier.ActiveTasks(subscriber, activeTaskInfo);

        return activeTaskInfo;
    }

    public async Task CreateHealthCheckReport(Subscriber subscriber, string url, string token, DateTime startDate,
        double threshold, ILogger log)
    {
        var timeReport = await GetTimeReport(subscriber, url, token, startDate, log, null);

        log.LogInformation("Query Result: totalActive:'{TotalActive}', totalEstimated:'{TotalEstimated}', totalCompleted:'{TotalCompleted}', expected: '{Expected}'",
            timeReport.TotalActive, timeReport.TotalEstimated, timeReport.TotalCompleted, timeReport.Expected);

        await _notifier.SendDetailedTimeReport(subscriber, timeReport, threshold);
    }

    public async Task CreateStandupReport(Subscriber subscriber, string url, string token, ILogger log)
    {
        var startDate = PreviousWorkDay(DateTime.Today);
        var timeReport = await GetTimeReport(subscriber, url, token, startDate, log, null);
        await _notifier.SendDetailedTimeReport(subscriber, timeReport, 0, includeSummary: false);
    }

    private async Task<TimeReport> GetTimeReport(Subscriber subscriber, string url, string token,
        DateTime startDate, ILogger log, DateTime? endDate)
    {
        var orgUrl = new Uri(url);
        var connection = new VssConnection(orgUrl, new VssBasicCredential(string.Empty, token));
        var workItemsIds = await _backlogAccessor.GetWorkItemsForPeriod(connection, subscriber.Email, startDate, endDate, log);
        var workItems = await _backlogAccessor.GetWorkItemsById(connection, workItemsIds);

        var report = new TimeReport();
        foreach (var workItem in workItems)
        {
            if (workItem.Id == null) continue;
            var workItemId = workItem.Id.Value;
            var activeTime = await backlogAccessor.GetWorkItemActiveTime(connection, workItemId);

            if (!workItem.Fields.TryGetValue<double>("Microsoft.VSTS.Scheduling.OriginalEstimate", out var estimated))
            {
                estimated = 0;
            }

            if (!workItem.Fields.TryGetValue<double>("Microsoft.VSTS.Scheduling.CompletedWork", out var completed))
            {
                completed = 0;
            }

            var workItemDate = workItem.Fields.Keys.Contains("Microsoft.VSTS.Common.ClosedDate")
                ? (DateTime)workItem.Fields["Microsoft.VSTS.Common.ClosedDate"]
                : (DateTime)workItem.Fields["System.ChangedDate"];


            var item = new WorkItemTime
            {
                Id = workItem.Id.GetValueOrDefault(),
                Date = workItemDate,
                Title = workItem.Fields["System.Title"].ToString(),
                Active = activeTime.TotalHours,
                Estimated = estimated,
                Completed = completed,
            };
            report.AddWorkItem(item);

            log.LogInformation("{Title} {Estimated:F2} {Completed:F2} {Active:F2}", item.Title, item.Estimated, item.Completed, item.Active);
        }

        report.StartDate = startDate;
        var reportEndDate = endDate ?? DateTime.UtcNow.Date;
        report.EndDate = reportEndDate;
        report.Expected = GetBusinessDays(startDate, reportEndDate) * (subscriber.HoursPerDay == 0 ? HoursPerDay : subscriber.HoursPerDay);
        report.HoursOff = GetHoursOff(subscriber, startDate);
        return report;
    }

    private static int GetHoursOff(Subscriber subscriber, DateTime startDate)
    {
        if (subscriber.TimeOff == null)
        {
            return 0;
        }

        var hoursOff = subscriber.TimeOff.Where(i => i.Date >= startDate).Sum(i => i.HoursOff);

        return hoursOff;
    }

    private Task SendReport(Subscriber subscriber, TimeReport timeReport, ILogger log)
    {
        log.LogInformation("Sending info.");
        return _notifier.SendTimeReport(subscriber, timeReport);
    }

    private static DateTime PreviousWorkDay(DateTime date)
    {
        do
        {
            date = date.AddDays(-1);
        }
        while (IsWeekend(date));

        return date;
    }

    private static bool IsWeekend(DateTime date) =>
        date.DayOfWeek == DayOfWeek.Saturday ||
        date.DayOfWeek == DayOfWeek.Sunday;

    private static double GetBusinessDays(DateTime startDate, DateTime endDate)
    {
        var calcBusinessDays =
            1 + ((endDate - startDate).TotalDays * 5 -
                 (startDate.DayOfWeek - endDate.DayOfWeek) * 2) / 7;

        if (endDate.DayOfWeek == DayOfWeek.Saturday) calcBusinessDays--;
        if (startDate.DayOfWeek == DayOfWeek.Sunday) calcBusinessDays--;

        return calcBusinessDays;
    }
}