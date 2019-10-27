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

        public MonitoringService(INotifier notifier, IBacklogAccessor backlogAccessor)
        {
            _notifier = Arg.NotNull(notifier, nameof(notifier));
            _backlogAccessor = Arg.NotNull(backlogAccessor, nameof(backlogAccessor));
        }

        public async Task PerformMonitoring(Subscriber subscriber, string url, string token, ILogger log)
        {
            var orgUrl = new Uri(url);
            var personalAccessToken = token;

            var connection = new VssConnection(orgUrl, new VssBasicCredential(string.Empty, personalAccessToken));
            var activeTaskInfo = await _backlogAccessor.GetActiveWorkItems(connection, subscriber.Email, log);
            await CreateAlertIfNeeded(subscriber, activeTaskInfo, log);
        }

        private async Task CreateAlertIfNeeded(Subscriber subscriber, ActiveTaskInfo activeTaskInfo, ILogger log)
        {
            var now = DateTime.UtcNow.TimeOfDay;

            if (now > subscriber.StartWorkingHoursUtc && now < subscriber.EndWorkingHoursUtc && IsWeekDay())
            {
                log.LogInformation($"It's working hours for {subscriber.Email}");
                if (!activeTaskInfo.HasActiveTasks)
                {
                    log.LogInformation($"No active tasks during working hours.");
                    await _notifier.NoActiveTasksDuringWorkingHours(subscriber);
                }
            }
            else
            {
                log.LogInformation($"It's not working hours for {subscriber.Email}");
                if (activeTaskInfo.HasActiveTasks)
                {
                    log.LogWarning($"There is an active task outside of working hours.");
                    await _notifier.ActiveTaskOutsideOfWorkingHours(subscriber);
                }
            }

            if (activeTaskInfo.ActiveTaskCount > 1)
            {
                log.LogInformation(
                    $"{activeTaskInfo.ActiveTaskCount} active tasks at the same time.");
                await _notifier.MoreThanSingleTaskIsActive(subscriber);
            }
        }

        private bool IsWeekDay()
        {
            return DateTime.UtcNow.DayOfWeek != DayOfWeek.Saturday && DateTime.UtcNow.DayOfWeek != DayOfWeek.Sunday;
        }

    }
}
