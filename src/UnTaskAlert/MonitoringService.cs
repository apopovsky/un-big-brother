using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using Microsoft.Extensions.Logging;
using UnTaskAlert.Common;
using UnTaskAlert.Models;

namespace UnTaskAlert
{
    public class MonitoringService(INotifier notifier, IBacklogAccessor backlogAccessor, IDbAccessor dbAccessor)
            : IMonitoringService
    {
        private readonly INotifier _notifier = Arg.NotNull(notifier, nameof(notifier));
        private readonly IBacklogAccessor _backlogAccessor = Arg.NotNull(backlogAccessor, nameof(backlogAccessor));
        private readonly IDbAccessor _dbAccessor = Arg.NotNull(dbAccessor, nameof(dbAccessor));
        private static readonly TimeSpan PauseBetweenAlerts = TimeSpan.FromMinutes(30);

        public async Task PerformMonitoring(Subscriber subscriber, string url, string token, ILogger log, CancellationToken cancellationToken)
        {
            var orgUrl = new Uri(url);

            if (subscriber.StartWorkingHoursUtc == default || subscriber.EndWorkingHoursUtc == default)
            {
                log.LogInformation("{Email} has no working hours set. Active task monitoring is disabled.", subscriber.Email);
                return;
            }

            var connection = new VssConnection(orgUrl, new VssBasicCredential(string.Empty, token));
            var activeTaskInfo = await _backlogAccessor.GetActiveWorkItems(connection, subscriber.Email, log);
            foreach (var taskInfo in activeTaskInfo.TasksInfo)
            {
                taskInfo.ActiveTime = (await backlogAccessor.GetWorkItemActiveTime(connection, taskInfo.Id)).TotalHours;
            }

            await CreateAlertIfNeeded(subscriber, activeTaskInfo, log, cancellationToken);
        }

        private async Task CreateAlertIfNeeded(Subscriber subscriber, ActiveTasksInfo activeTasksInfo, ILogger log, CancellationToken cancellationToken)
        {
            if (subscriber.SnoozeAlertsUntil.GetValueOrDefault(DateTime.MinValue) > DateTime.UtcNow)
            {
                log.LogInformation("Alert checks snoozed for subscriber {Email} til {SnoozeAlertsUntil:G}", subscriber.Email, subscriber.SnoozeAlertsUntil);
                return;
            }

            var now = DateTime.UtcNow.TimeOfDay;
            if (now > subscriber.StartWorkingHoursUtc && now < subscriber.EndWorkingHoursUtc && IsWeekDay())
            {
                log.LogInformation("It's working hours for {Email}", subscriber.Email);
                if (!activeTasksInfo.HasActiveTasks)
                {
                    log.LogInformation("No active tasks during working hours.");
                    if (DateTime.UtcNow - subscriber.LastNoActiveTasksAlert >= PauseBetweenAlerts)
                    {
                        subscriber.LastNoActiveTasksAlert = DateTime.UtcNow;
                        await _notifier.NoActiveTasksDuringWorkingHours(subscriber);
                    }
                }
            }
            else
            {
                log.LogInformation("It's not working hours for {Email}", subscriber.Email);
                if (activeTasksInfo.HasActiveTasks && DateTime.UtcNow - subscriber.LastActiveTaskOutsideOfWorkingHoursAlert >= PauseBetweenAlerts)
                {
                    log.LogWarning("There is an active task outside of working hours.");
                    subscriber.LastActiveTaskOutsideOfWorkingHoursAlert = DateTime.UtcNow;
                    await _notifier.ActiveTaskOutsideOfWorkingHours(subscriber, activeTasksInfo);
                }
            }

            if (activeTasksInfo.ActiveTaskCount > 1 && DateTime.UtcNow - subscriber.LastMoreThanSingleTaskIsActiveAlert >= PauseBetweenAlerts)
            {
                log.LogInformation("{ActiveTaskCount} active tasks at the same time.", activeTasksInfo.ActiveTaskCount);
                subscriber.LastMoreThanSingleTaskIsActiveAlert = DateTime.UtcNow;

                await _notifier.MoreThanSingleTaskIsActive(subscriber, activeTasksInfo);
            }

            await _dbAccessor.AddOrUpdateSubscriber(subscriber, cancellationToken);
        }

        private bool IsWeekDay() => DateTime.UtcNow.DayOfWeek != DayOfWeek.Saturday && DateTime.UtcNow.DayOfWeek != DayOfWeek.Sunday;
    }
}
