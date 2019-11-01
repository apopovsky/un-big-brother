using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using UnTaskAlert.Models;

namespace UnTaskAlert
{
	public interface IReportingService
	{
		Task CreateWorkHoursReport(Subscriber subscriber, string url, string token, DateTime startDate, ILogger log);
        Task<ActiveTaskInfo> ActiveTasksReport(Subscriber subscriber, string url, string token, DateTime startDate, ILogger log);
        Task CreateHealthCheckReport(Subscriber subscriber, string url, string token, DateTime startDate, double threshold, ILogger log);

    }
}