using System;
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

        public async Task PerformMonitoring(Subscriber subscriber, string url, string token, ILogger log)
        {
            var orgUrl = new Uri(url);
            var personalAccessToken = token;

            if (subscriber.StartWorkingHoursUtc == default || subscriber.EndWorkingHoursUtc == default)
            {
                log.LogInformation($"{subscriber.Email} has no working hours set. Active task monitoring is disabled.");

                return;
            }

            var connection = new VssConnection(orgUrl, new VssBasicCredential(string.Empty, personalAccessToken));
            var activeTaskInfo = await _backlogAccessor.GetActiveWorkItems(connection, subscriber.Email, log);
            await CreateAlertIfNeeded(subscriber, activeTaskInfo, log);
        }

        private async Task CreateAlertIfNeeded(Subscriber subscriber, ActiveTaskInfo activeTaskInfo, ILogger log)
        {
            var now = DateTime.UtcNow.TimeOfDay;

            if (now > subscriber.StartWorkingHoursUtc && now < subscriber.EndWorkingHoursUtc
                && IsWeekDay()
                && DateTime.UtcNow - subscriber.LastNoActiveTasksAlert <= PauseBetweenAlerts)
            {
                log.LogInformation($"It's working hours for {subscriber.Email}");
                if (!activeTaskInfo.HasActiveTasks)
                {
                    log.LogInformation($"No active tasks during working hours.");
                    subscriber.LastNoActiveTasksAlert = DateTime.UtcNow;
                    await _notifier.NoActiveTasksDuringWorkingHours(subscriber);
                }
            }
            else
            {
                log.LogInformation($"It's not working hours for {subscriber.Email}");
                if (activeTaskInfo.HasActiveTasks
                    && DateTime.UtcNow - subscriber.LastActiveTaskOutsideOfWorkingHoursAlert <= PauseBetweenAlerts)
                {
                    log.LogWarning($"There is an active task outside of working hours.");
                    subscriber.LastActiveTaskOutsideOfWorkingHoursAlert = DateTime.UtcNow;
                    await _notifier.ActiveTaskOutsideOfWorkingHours(subscriber, activeTaskInfo);
                }
            }

            if (activeTaskInfo.ActiveTaskCount > 1
                && DateTime.UtcNow - subscriber.LastMoreThanSingleTaskIsActiveAlert <= PauseBetweenAlerts)
            {
                log.LogInformation(
                    $"{activeTaskInfo.ActiveTaskCount} active tasks at the same time.");
                subscriber.LastMoreThanSingleTaskIsActiveAlert = DateTime.UtcNow;
                await _notifier.MoreThanSingleTaskIsActive(subscriber);
            }

            await _dbAccessor.AddOrUpdateSubscriber(subscriber);
        }

        private bool IsWeekDay()
        {
            return DateTime.UtcNow.DayOfWeek != DayOfWeek.Saturday && DateTime.UtcNow.DayOfWeek != DayOfWeek.Sunday;
        }

    }
}
