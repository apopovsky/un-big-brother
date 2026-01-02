using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using UnTaskAlert.Common;
using UnTaskAlert.Models;

namespace UnTaskAlert;

public class ReportingService(INotifier notifier, IBacklogAccessor backlogAccessor, IPrAccessor prAccessor) : IReportingService
{
    public const int HoursPerDay = 8;

    private readonly INotifier _notifier = Arg.NotNull(notifier, nameof(notifier));
    private readonly IBacklogAccessor _backlogAccessor = Arg.NotNull(backlogAccessor, nameof(backlogAccessor));
    private readonly IPrAccessor _prAccessor = Arg.NotNull(prAccessor, nameof(prAccessor));

    public async Task CreateWorkHoursReport(Subscriber subscriber, string url, string token, DateTime startDate,
        ILogger log, DateTime? endDate)
    {
        var report = await GetTimeReport(subscriber, url, token, startDate, log, endDate);

        log.LogInformation("Query Result: totalActive:'{TotalActive}', totalEstimated:'{TotalEstimated}', totalCompleted:'{TotalCompleted}', expected: '{Expected}'",
            report.TotalActive, report.TotalEstimated, report.TotalCompleted, report.Expected);

        // Check if this is a single day report
        var isSingleDayReport = endDate.HasValue ? startDate.Date == endDate.Value.Date : startDate.Date == DateTime.Today;

        if (isSingleDayReport)
        {
            // For single day reports, provide a detailed view similar to standup
            await _notifier.SendDetailedTimeReport(subscriber, report, 0, includeSummary: true);
        }
        await SendReport(subscriber, report, log);
    }

    public async Task<ActiveTasksInfo> ActiveTasksReport(Subscriber subscriber, string url, string token, DateTime startDate, ILogger log)
    {
        var orgUrl = new Uri(url);

        var connection = new VssConnection(orgUrl, new VssBasicCredential(string.Empty, token));
        var activeTaskInfo = await _backlogAccessor.GetActiveWorkItems(connection, subscriber.Email, log);
        foreach (var taskInfo in activeTaskInfo.TasksInfo)
        {
            taskInfo.ActiveTime = (await _backlogAccessor.GetWorkItemActiveTime(connection, taskInfo.Id)).TotalHours;
            var parent = await _backlogAccessor.GetParentUserStory(connection, taskInfo.Id);
            taskInfo.Parent = new TaskInfo(parent);
        }

        await _notifier.ActiveTasks(subscriber, activeTaskInfo);

        return activeTaskInfo;
    }

    public async Task<ActivePullRequestsInfo> ActivePullRequestsReport(Subscriber subscriber, string url, string token, ILogger log)
    {
        var orgUrl = new Uri(url);
        var connection = new VssConnection(orgUrl, new VssBasicCredential(string.Empty, token));
        var activePrInfo = await _prAccessor.GetActivePullRequests(connection, url, subscriber.Email, subscriber.AzureDevOpsProjects, log);

        await _notifier.ActivePullRequests(subscriber, activePrInfo);

        return activePrInfo;
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

    public async Task StoryInfoReport(Subscriber subscriber, string url, string token, int storyId, ILogger log)
    {
        long.TryParse(subscriber.TelegramId, out var chatId);

        var orgUrl = new Uri(url);
        var connection = new VssConnection(orgUrl, new VssBasicCredential(string.Empty, token));
        var story = await _backlogAccessor.GetWorkItemsById(connection, [storyId], Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models.WorkItemExpand.Relations);
        if (story == null || story.Count == 0)
        {
            await _notifier.Respond(chatId, $"No se encontró la User Story con id {storyId}.");
            return;
        }
        var userStory = story[0];
        string assignedToUniqueName = null;
        string assignedToDisplayName = null;
        if (userStory.Fields.TryGetValue("System.AssignedTo", out var assignedToObj) && assignedToObj != null)
        {
            if (assignedToObj is IdentityRef identity)
            {
                assignedToUniqueName = identity.UniqueName;
                assignedToDisplayName = identity.DisplayName;
            }
            else
            {
                assignedToUniqueName = assignedToObj.ToString();
            }
        }
        if (string.IsNullOrEmpty(assignedToUniqueName) && string.IsNullOrEmpty(assignedToDisplayName))
        {
            await _notifier.Respond(chatId, "La User Story no tiene usuario asignado.");
            return;
        }
        var childIds = _backlogAccessor.GetChildTaskIds(connection, userStory);
        if (childIds == null || childIds.Count == 0)
        {
            await _notifier.Respond(chatId, "La User Story no tiene child tasks.");
            return;
        }
        var childTasks = await _backlogAccessor.GetWorkItemsById(connection, childIds);
        var assignedTasks = childTasks.Where(t =>
        {
            string childUniqueName = null;
            string childDisplayName = null;
            if (t.Fields.TryGetValue("System.AssignedTo", out var childAssignedToObj) && childAssignedToObj != null)
            {
                if (childAssignedToObj is IdentityRef identity)
                {
                    childUniqueName = identity.UniqueName;
                    childDisplayName = identity.DisplayName;
                }
                else
                {
                    childUniqueName = childAssignedToObj.ToString();
                }
            }
            return (!string.IsNullOrEmpty(childUniqueName) && childUniqueName == assignedToUniqueName) ||
                   (!string.IsNullOrEmpty(childDisplayName) && childDisplayName == assignedToDisplayName);
        }).ToList();
        if (assignedTasks.Count == 0)
        {
            await _notifier.Respond(chatId, "No hay child tasks asignadas al mismo usuario que la User Story.");
            return;
        }
        var results = new List<string>();
        TimeSpan totalActive = TimeSpan.Zero;
        foreach (var task in assignedTasks)
        {
            var activeTime = await _backlogAccessor.GetWorkItemActiveTime(connection, task.Id.Value);
            totalActive += activeTime;
            results.Add($"- {task.Id}: {task.Fields["System.Title"]} | Tiempo activo: {activeTime.TotalHours:F2}h");
        }
        var msg = $"User Story {storyId} ({userStory.Fields["System.Title"]})\nUsuario asignado: {(assignedToDisplayName ?? assignedToUniqueName)}\n\nChild tasks asignadas a este usuario:\n" + string.Join("\n", results) + $"\n\nTiempo activo total: {totalActive.TotalHours:F2}h";
        await _notifier.Respond(chatId, msg);
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
            var activeTime = await _backlogAccessor.GetWorkItemActiveTime(connection, workItemId);

            if (!workItem.Fields.TryGetValue<double>("Microsoft.VSTS.Scheduling.OriginalEstimate", out var estimated))
            {
                estimated = 0;
            }

            if (!workItem.Fields.TryGetValue<double>("Microsoft.VSTS.Scheduling.CompletedWork", out var completed))
            {
                completed = 0;
            }

            var workItemDate = workItem.Fields.TryGetValue("Microsoft.VSTS.Common.ClosedDate", out var field) ? (DateTime)field
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
        while (date.IsWeekend());

        return date;
    }

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