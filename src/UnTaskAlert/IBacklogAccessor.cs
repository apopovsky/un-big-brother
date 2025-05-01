using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.WebApi;
using UnTaskAlert.Models;

namespace UnTaskAlert;

public interface IBacklogAccessor
{
    Task<ActiveTasksInfo> GetActiveWorkItems(VssConnection connection, string username, ILogger log);
    Task<List<int>> GetWorkItemsForPeriod(VssConnection connection, string username, DateTime dateTime,
        DateTime? dateTo, ILogger log);
    Task<IList<WorkItem>> GetWorkItemsById(VssConnection connection, List<int> workItemsIds);
    Task<IList<WorkItemUpdate>> GetWorkItemUpdates(VssConnection connection, int workItemId);
    Task<TimeSpan> GetWorkItemActiveTime(VssConnection connection, int workItemId);
    Task<WorkItem> GetParentUserStory(VssConnection connection, int workItemId);
}