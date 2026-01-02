using Microsoft.Extensions.Logging;
using UnTaskAlert.Models;

namespace UnTaskAlert;

public interface IReportingService
{
    Task CreateWorkHoursReport(Subscriber subscriber, string url, string token, DateTime startDate, ILogger log, DateTime? endDate);
    Task<ActiveTasksInfo> ActiveTasksReport(Subscriber subscriber, string url, string token, DateTime startDate, ILogger log);
    Task<ActivePullRequestsInfo> ActivePullRequestsReport(Subscriber subscriber, string url, string token, ILogger log);
    Task CreateHealthCheckReport(Subscriber subscriber, string url, string token, DateTime startDate, double threshold, ILogger log);
    Task CreateStandupReport(Subscriber subscriber, string url, string token, ILogger log);
    Task StoryInfoReport(Subscriber subscriber, string url, string token, int storyId, Microsoft.Extensions.Logging.ILogger log);
}