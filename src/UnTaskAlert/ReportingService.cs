using System;
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
        private static readonly int hoursPerDay = 8;

        private readonly INotifier _notifier;
		private readonly IBacklogAccessor _backlogAccessor;

		public ReportingService(INotifier notifier, IBacklogAccessor backlogAccessor)
		{
			_notifier = Arg.NotNull(notifier, nameof(notifier));
			_backlogAccessor = Arg.NotNull(backlogAccessor, nameof(backlogAccessor));
		}

		public async Task CreateWorkHoursReport(Subscriber subscriber, string url, string token, DateTime startDate, ILogger log)
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
					var updates = await _backlogAccessor.GetWorkItemUpdates(connection, workItem.Id.Value);
					DateTime? activeStart = null;
					TimeSpan activeTime = TimeSpan.Zero;
					foreach (var itemUpdate in updates)
					{
						if (itemUpdate.Fields == null || !itemUpdate.Fields.ContainsKey("System.State")) continue;
						if (itemUpdate.Fields["System.State"].NewValue.ToString() == "Active")
						{
							activeStart = (DateTime)itemUpdate.Fields["System.ChangedDate"].NewValue;
						}

						if (activeStart.HasValue && itemUpdate.Fields["System.State"].NewValue.ToString() != "Active")
						{
							var activeEnd = (DateTime)itemUpdate.Fields["System.ChangedDate"].NewValue;
							var span = activeEnd - activeStart;
							activeTime = activeTime.Add(span.GetValueOrDefault());
							activeStart = null;
						}
					}

					var item = new WorkItemTime
					{
						Title = workItem.Fields["System.Title"].ToString(),
						Active = activeTime.TotalHours,
						Estimated = (double)workItem.Fields["Microsoft.VSTS.Scheduling.OriginalEstimate"],
						Completed = (double)workItem.Fields["Microsoft.VSTS.Scheduling.CompletedWork"]
					};
					report.AddWorkItem(item);
                    report.StartDate = startDate;

					log.LogInformation($"{item.Title} {item.Estimated:F2} {item.Completed:F2} {item.Active:F2}");
				}
			}

            report.Expected = GetBusinessDays(startDate, DateTime.UtcNow.Date) * hoursPerDay;

            log.LogInformation($"Query Result: totalActive:'{report.TotalActive}', totalEstimated:'{report.TotalEstimated}', totalCompleted:'{report.TotalCompleted}', expected: '{report.Expected}'");

            await SendReport(subscriber, report, log);
		}

        public async Task<ActiveTaskInfo> ActiveTasksReport(Subscriber subscriber, string url, string token, DateTime startDate, ILogger log)
        {
            var orgUrl = new Uri(url);
            var personalAccessToken = token;

            var connection = new VssConnection(orgUrl, new VssBasicCredential(string.Empty, personalAccessToken));
            var activeTaskInfo = await _backlogAccessor.GetActiveWorkItems(connection, subscriber.Email, log);

            await _notifier.ActiveTasks(subscriber, activeTaskInfo);

            return activeTaskInfo;
        }

        private async Task SendReport(Subscriber subscriber, TimeReport timeReport, ILogger log)
		{
			log.LogWarning($"Sending info.");
			await _notifier.SendTimeReport(subscriber, timeReport);
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