using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using UnTaskAlert.Common;
using UnTaskAlert.Models;

namespace UnTaskAlert
{
    public class ReportingService : IReportingService
    {
        public static readonly int HoursPerDay = 8;

        private readonly INotifier _notifier;
        private readonly IBacklogAccessor _backlogAccessor;

        public ReportingService(INotifier notifier, IBacklogAccessor backlogAccessor)
        {
            _notifier = Arg.NotNull(notifier, nameof(notifier));
            _backlogAccessor = Arg.NotNull(backlogAccessor, nameof(backlogAccessor));
        }

        public async Task CreateWorkHoursReport(Subscriber subscriber, string url, string token, DateTime startDate, ILogger log)
        {
            var report = await GetTimeReport(subscriber, url, token, startDate, log);

            log.LogInformation($"Query Result: totalActive:'{report.TotalActive}', totalEstimated:'{report.TotalEstimated}', totalCompleted:'{report.TotalCompleted}', expected: '{report.Expected}'");

            await SendReport(subscriber, report, log);
        }

        public async Task<ActiveTasksInfo> ActiveTasksReport(Subscriber subscriber, string url, string token, DateTime startDate, ILogger log)
        {
            var orgUrl = new Uri(url);
            var personalAccessToken = token;

            var connection = new VssConnection(orgUrl, new VssBasicCredential(string.Empty, personalAccessToken));
            var activeTaskInfo = await _backlogAccessor.GetActiveWorkItems(connection, subscriber.Email, log);
            foreach (var taskInfo in activeTaskInfo.TasksInfo)
            {
                taskInfo.ActiveTime = (await GetWorkItemActiveTime(connection, taskInfo.Id)).TotalHours;
            }

            await _notifier.ActiveTasks(subscriber, activeTaskInfo);

            return activeTaskInfo;
        }

        public async Task CreateHealthCheckReport(Subscriber subscriber, string url, string token, DateTime startDate, double threshold, ILogger log)
        {
            var timeReport = await GetTimeReport(subscriber, url, token, startDate, log);

            log.LogInformation($"Query Result: totalActive:'{timeReport.TotalActive}', totalEstimated:'{timeReport.TotalEstimated}', totalCompleted:'{timeReport.TotalCompleted}', expected: '{timeReport.Expected}'");
            
            await _notifier.SendDetailedTimeReport(subscriber, timeReport, threshold);
        }

        public async Task CreateStandupReport(Subscriber subscriber, string url, string token, ILogger log)
        {
            var startDate = PreviousWorkDay(DateTime.Today);
            var timeReport = await GetTimeReport(subscriber, url, token, startDate, log);
            await _notifier.SendDetailedTimeReport(subscriber, timeReport, 0, includeSummary: false);
        }

        private async Task<TimeReport> GetTimeReport(Subscriber subscriber, string url, string token, DateTime startDate, ILogger log)
        {
            var orgUrl = new Uri(url);
            var personalAccessToken = token;

            var connection = new VssConnection(orgUrl, new VssBasicCredential(string.Empty, personalAccessToken));
            var workItemsIds = await _backlogAccessor.GetWorkItemsForPeriod(connection, subscriber.Email, startDate, log);
            var workItems = await _backlogAccessor.GetWorkItemsById(connection, workItemsIds);

            var report = new TimeReport();
            foreach (var workItem in workItems)
            {
                if (workItem.Id != null)
                {
                    var workItemId = workItem.Id.Value;
                    var activeTime = await GetWorkItemActiveTime(connection, workItemId);

                    if (!workItem.Fields.TryGetValue<double>("Microsoft.VSTS.Scheduling.OriginalEstimate", out var estimated))
                    {
                        estimated = 0;
                    }

                    if (!workItem.Fields.TryGetValue<double>("Microsoft.VSTS.Scheduling.CompletedWork", out var completed))
                    {
                        completed = 0;
                    }

                    var workItemDate = workItem.Fields.Keys.Contains("Microsoft.VSTS.Common.ClosedDate")
                        ? (DateTime) workItem.Fields["Microsoft.VSTS.Common.ClosedDate"]
                        : (DateTime) workItem.Fields["System.ChangedDate"];


                    var item = new WorkItemTime
                    {
                        Id = workItem.Id.GetValueOrDefault(),
                        Date = workItemDate,
                        Title = workItem.Fields["System.Title"].ToString(),
                        Active = activeTime.TotalHours,
                        Estimated = estimated,
                        Completed = completed
                    };
                    report.AddWorkItem(item);

                    log.LogInformation($"{item.Title} {item.Estimated:F2} {item.Completed:F2} {item.Active:F2}");
                }
            }

            report.StartDate = startDate;
            report.EndDate = DateTime.UtcNow.Date;
            report.Expected = GetBusinessDays(startDate, DateTime.UtcNow.Date) * (subscriber.HoursPerDay == 0 ? HoursPerDay : subscriber.HoursPerDay);
            report.HoursOff = GetHoursOff(subscriber, startDate);
            return report;
        }

        private int GetHoursOff(Subscriber subscriber, DateTime startDate)
        {
            if (subscriber.TimeOff == null)
            {
                return 0;
            }

            var hoursOff = subscriber.TimeOff.Where(i => i.Date >= startDate).Sum(i => i.HoursOff);

            return hoursOff;
        }

        private async Task<TimeSpan> GetWorkItemActiveTime(VssConnection connection, int workItemId)
        {
            var updates = await _backlogAccessor.GetWorkItemUpdates(connection, workItemId);
            DateTime? activeStart = null;
            TimeSpan activeTime = TimeSpan.Zero;
            foreach (var itemUpdate in updates)
            {
                if (itemUpdate.Fields == null || !itemUpdate.Fields.ContainsKey("System.State")) continue;
                if (itemUpdate.Fields["System.State"].NewValue.ToString() == "Active")
                {
                    activeStart = (DateTime) itemUpdate.Fields["System.ChangedDate"].NewValue;
                }

                if (activeStart.HasValue && itemUpdate.Fields["System.State"].NewValue.ToString() != "Active")
                {
                    var activeEnd = (DateTime) itemUpdate.Fields["System.ChangedDate"].NewValue;
                    var span = activeEnd - activeStart;
                    activeTime = activeTime.Add(span.GetValueOrDefault());
                    activeStart = null;
                }
            }

            //Add running active time to current active task
            if (activeStart.HasValue)
            {
                var span = DateTime.UtcNow - activeStart;
                activeTime = activeTime.Add(span.GetValueOrDefault());
            }

            return activeTime;
        }

        private async Task SendReport(Subscriber subscriber, TimeReport timeReport, ILogger log)
        {
            log.LogInformation($"Sending info.");
            await _notifier.SendTimeReport(subscriber, timeReport);
        }

        public DateTime PreviousWorkDay(DateTime date)
        {
            do
            {
                date = date.AddDays(-1);
            }
            while (IsWeekend(date));

            return date;
        }

        private bool IsWeekend(DateTime date)
        {
            return date.DayOfWeek == DayOfWeek.Saturday ||
                   date.DayOfWeek == DayOfWeek.Sunday;
        }

        private static double GetBusinessDays(DateTime startDate, DateTime endDate)
        {
            double calcBusinessDays =
                1 + ((endDate - startDate).TotalDays * 5 -
                     (startDate.DayOfWeek - endDate.DayOfWeek) * 2) / 7;

            if (endDate.DayOfWeek == DayOfWeek.Saturday) calcBusinessDays--;
            if (startDate.DayOfWeek == DayOfWeek.Sunday) calcBusinessDays--;

            return calcBusinessDays;
        }
    }
}