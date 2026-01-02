using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using Microsoft.Extensions.Logging;
using UnTaskAlert.Common;
using UnTaskAlert.Models;

namespace UnTaskAlert;

public class MonitoringService(INotifier notifier, IBacklogAccessor backlogAccessor, IPrAccessor prAccessor, IDbAccessor dbAccessor)
    : IMonitoringService
{
    private readonly INotifier _notifier = Arg.NotNull(notifier, nameof(notifier));
    private readonly IBacklogAccessor _backlogAccessor = Arg.NotNull(backlogAccessor, nameof(backlogAccessor));
    private readonly IPrAccessor _prAccessor = Arg.NotNull(prAccessor, nameof(prAccessor));
    private readonly IDbAccessor _dbAccessor = Arg.NotNull(dbAccessor, nameof(dbAccessor));
    private static readonly TimeSpan pauseBetweenAlerts = TimeSpan.FromMinutes(30);

    public async Task PerformMonitoring(Subscriber subscriber, string url, string token, ILogger log, CancellationToken cancellationToken)
    {
        var orgUrl = new Uri(url);

        if (subscriber.StartWorkingHoursUtc == TimeSpan.Zero || subscriber.EndWorkingHoursUtc == TimeSpan.Zero)
        {
            log.LogInformation("{Email} has no working hours set. Active task monitoring is disabled.", subscriber.Email);
            return;
        }

        var connection = new VssConnection(orgUrl, new VssBasicCredential(string.Empty, token));
        var activeTaskInfo = await _backlogAccessor.GetActiveWorkItems(connection, subscriber.Email, log);
        foreach (var taskInfo in activeTaskInfo.TasksInfo)
        {
            taskInfo.ActiveTime = (await _backlogAccessor.GetWorkItemActiveTime(connection, taskInfo.Id)).TotalHours;
        }

        await CreateAlertIfNeeded(subscriber, activeTaskInfo, log, cancellationToken);

        await CreatePullRequestAlertIfNeeded(subscriber, connection, url, log, cancellationToken);
    }

    private async Task CreatePullRequestAlertIfNeeded(Subscriber subscriber, VssConnection connection, string azureDevOpsAddress, ILogger log, CancellationToken cancellationToken)
    {
        if (subscriber.SnoozeAlertsUntil.GetValueOrDefault(DateTime.MinValue) > DateTime.UtcNow)
        {
            return;
        }

        if (subscriber.AzureDevOpsProjects == null || subscriber.AzureDevOpsProjects.Count == 0)
        {
            return;
        }

        if (!TryGetPrAlertSlot(subscriber, DateTime.UtcNow, out var slot))
        {
            return;
        }

        var slotHourUtc = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, DateTime.UtcNow.Day, DateTime.UtcNow.Hour, 0, 0, DateTimeKind.Utc);
        if (slot == 1 && subscriber.LastActivePullRequestsStartSlotAlertUtc == slotHourUtc)
        {
            return;
        }
        if (slot == 2 && subscriber.LastActivePullRequestsMidSlotAlertUtc == slotHourUtc)
        {
            return;
        }

        var prs = await _prAccessor.GetActivePullRequests(connection, azureDevOpsAddress, subscriber.Email, subscriber.AzureDevOpsProjects, log);
        if (!prs.HasActivePullRequests)
        {
            log.LogInformation("No active pull requests for {Email} in PR alert slot {Slot}.", subscriber.Email, slot);
            return;
        }

        if (slot == 1)
        {
            subscriber.LastActivePullRequestsStartSlotAlertUtc = slotHourUtc;
        }
        else
        {
            subscriber.LastActivePullRequestsMidSlotAlertUtc = slotHourUtc;
        }

        await _notifier.ActivePullRequestsReminder(subscriber, prs);
        await _dbAccessor.AddOrUpdateSubscriber(subscriber, cancellationToken);
    }

    private static bool TryGetPrAlertSlot(Subscriber subscriber, DateTime utcNow, out int slot)
    {
        slot = 0;
        if (!utcNow.IsWeekDay())
        {
            return false;
        }

        var now = utcNow.TimeOfDay;
        if (now <= subscriber.StartWorkingHoursUtc || now >= subscriber.EndWorkingHoursUtc)
        {
            return false;
        }

        var slot1Start = subscriber.StartWorkingHoursUtc;
        var slot1End = slot1Start.Add(TimeSpan.FromHours(1));

        var slot2Start = subscriber.StartWorkingHoursUtc.Add(TimeSpan.FromHours(4));
        var slot2End = slot2Start.Add(TimeSpan.FromHours(1));

        if (now >= slot1Start && now < slot1End)
        {
            slot = 1;
            return true;
        }

        if (now >= slot2Start && now < slot2End)
        {
            slot = 2;
            return true;
        }

        return false;
    }

    private async Task CreateAlertIfNeeded(Subscriber subscriber, ActiveTasksInfo activeTasksInfo, ILogger log, CancellationToken cancellationToken)
    {
        if (subscriber.SnoozeAlertsUntil.GetValueOrDefault(DateTime.MinValue) > DateTime.UtcNow)
        {
            log.LogInformation("Alert checks snoozed for subscriber {Email} til {SnoozeAlertsUntil:G}", subscriber.Email, subscriber.SnoozeAlertsUntil);
            return;
        }

        var now = DateTime.UtcNow.TimeOfDay;
        if (now > subscriber.StartWorkingHoursUtc && now < subscriber.EndWorkingHoursUtc && DateTime.UtcNow.IsWeekDay())
        {
            log.LogInformation("It's working hours for {Email}", subscriber.Email);
            if (!activeTasksInfo.HasActiveTasks)
            {
                log.LogInformation("No active tasks during working hours.");
                if (DateTime.UtcNow - subscriber.LastNoActiveTasksAlert >= pauseBetweenAlerts)
                {
                    subscriber.LastNoActiveTasksAlert = DateTime.UtcNow;
                    await _notifier.NoActiveTasksDuringWorkingHours(subscriber);
                }
            }
        }
        else
        {
            log.LogInformation("It's not working hours for {Email}", subscriber.Email);
            if (activeTasksInfo.HasActiveTasks && DateTime.UtcNow - subscriber.LastActiveTaskOutsideOfWorkingHoursAlert >= pauseBetweenAlerts)
            {
                log.LogWarning("There is an active task outside of working hours.");
                subscriber.LastActiveTaskOutsideOfWorkingHoursAlert = DateTime.UtcNow;
                await _notifier.ActiveTaskOutsideOfWorkingHours(subscriber, activeTasksInfo);
            }
        }

        if (activeTasksInfo.ActiveTaskCount > 1 && DateTime.UtcNow - subscriber.LastMoreThanSingleTaskIsActiveAlert >= pauseBetweenAlerts)
        {
            log.LogInformation("{ActiveTaskCount} active tasks at the same time.", activeTasksInfo.ActiveTaskCount);
            subscriber.LastMoreThanSingleTaskIsActiveAlert = DateTime.UtcNow;

            await _notifier.MoreThanSingleTaskIsActive(subscriber, activeTasksInfo);
        }

        await _dbAccessor.AddOrUpdateSubscriber(subscriber, cancellationToken);
    }
}