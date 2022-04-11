using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using Microsoft.Extensions.Logging;
using UnTaskAlert.Common;
using UnTaskAlert.Models;

namespace UnTaskAlert
{
    public class MonitoringService : IMonitoringService
    {
        private readonly INotifier _notifier;
        private readonly IBacklogAccessor _backlogAccessor;
        private readonly IDbAccessor _dbAccessor;
        private static readonly TimeSpan PauseBetweenAlerts = TimeSpan.FromMinutes(30);

        public MonitoringService(INotifier notifier, IBacklogAccessor backlogAccessor, IDbAccessor dbAccessor)
        {
            _notifier = Arg.NotNull(notifier, nameof(notifier));
            _backlogAccessor = Arg.NotNull(backlogAccessor, nameof(backlogAccessor));
            _dbAccessor = Arg.NotNull(dbAccessor, nameof(dbAccessor));
        }

        public async Task PerformMonitoring(Subscriber subscriber, string url, string token, ILogger log, CancellationToken cancellationToken)
        {
            var orgUrl = new Uri(url);

            if (subscriber.StartWorkingHoursUtc == default || subscriber.EndWorkingHoursUtc == default)
            {
                log.LogInformation($"{subscriber.Email} has no working hours set. Active task monitoring is disabled.");

                return;
            }

            var connection = new VssConnection(orgUrl, new VssBasicCredential(string.Empty, token));
            var activeTaskInfo = await _backlogAccessor.GetActiveWorkItems(connection, subscriber.Email, log);
            await CreateAlertIfNeeded(subscriber, activeTaskInfo, log, cancellationToken);
        }

        private async Task CreateAlertIfNeeded(Subscriber subscriber, ActiveTasksInfo activeTasksInfo, ILogger log, CancellationToken cancellationToken)
        {
            if (subscriber.SnoozeAlertsUntil.GetValueOrDefault(DateTime.MinValue) > DateTime.UtcNow)
            {
                log.LogInformation($"Alert checks snoozed for subscriber {subscriber.Email} til {subscriber.SnoozeAlertsUntil:G}");
                return;
            }

            var now = DateTime.UtcNow.TimeOfDay;
            if (now > subscriber.StartWorkingHoursUtc && now < subscriber.EndWorkingHoursUtc
                && IsWeekDay())
            {
                log.LogInformation($"It's working hours for {subscriber.Email}");
                if (!activeTasksInfo.HasActiveTasks)
                {
                    log.LogInformation($"No active tasks during working hours.");
                    if (DateTime.UtcNow - subscriber.LastNoActiveTasksAlert >= PauseBetweenAlerts)
                    {
                        subscriber.LastNoActiveTasksAlert = DateTime.UtcNow;
                        await _notifier.NoActiveTasksDuringWorkingHours(subscriber);
                    }
                }
            }
            else
            {
                log.LogInformation($"It's not working hours for {subscriber.Email}");
                if (activeTasksInfo.HasActiveTasks
                    && DateTime.UtcNow - subscriber.LastActiveTaskOutsideOfWorkingHoursAlert >= PauseBetweenAlerts)
                {
                    log.LogWarning($"There is an active task outside of working hours.");
                    subscriber.LastActiveTaskOutsideOfWorkingHoursAlert = DateTime.UtcNow;
                    await _notifier.ActiveTaskOutsideOfWorkingHours(subscriber, activeTasksInfo);
                }
            }

            if (activeTasksInfo.ActiveTaskCount > 1
                && DateTime.UtcNow - subscriber.LastMoreThanSingleTaskIsActiveAlert >= PauseBetweenAlerts)
            {
                log.LogInformation($"{activeTasksInfo.ActiveTaskCount} active tasks at the same time.");
                subscriber.LastMoreThanSingleTaskIsActiveAlert = DateTime.UtcNow;
                await _notifier.MoreThanSingleTaskIsActive(subscriber, activeTasksInfo);
            }

            await _dbAccessor.AddOrUpdateSubscriber(subscriber, cancellationToken);
        }

        private bool IsWeekDay()
        {
            return DateTime.UtcNow.DayOfWeek != DayOfWeek.Saturday && DateTime.UtcNow.DayOfWeek != DayOfWeek.Sunday;
        }

    }
}
