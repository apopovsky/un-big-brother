﻿using System;
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
		private readonly INotifier _notifier;
		private readonly IBacklogAccessor _backlogAccessor;

		public ReportingService(INotifier notifier, IBacklogAccessor backlogAccessor)
		{
			_notifier = Arg.NotNull(notifier, nameof(notifier));
			_backlogAccessor = Arg.NotNull(backlogAccessor, nameof(backlogAccessor));
		}

		public async Task CreateReport(Subscriber subscriber, string url, string token, ILogger log)
		{
			var orgUrl = new Uri(url);
			var personalAccessToken = token;

			var connection = new VssConnection(orgUrl, new VssBasicCredential(string.Empty, personalAccessToken));
			var workItemsIds = await _backlogAccessor.GetWorkItemsForPeriod(connection, subscriber.Email, new DateTime(2019, 10, 1), log);
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

					log.LogInformation($"{item.Title} {item.Estimated:F2} {item.Completed:F2} {item.Active:F2}");
				}
			}

			log.LogInformation($"Query Result: toalActive:'{report.TotalActive}', totalEstimated:'{report.TotalEstimated}', totalCompleted:'{report.TotalCompleted}', ");

			await SendReport(subscriber, report, log);
		}
		
		private async Task SendReport(Subscriber subscriber, TimeReport timeReport, ILogger log)
		{
			log.LogWarning($"Sending info.");
			await _notifier.SendTimeReport(subscriber, timeReport);
		}
	}
}