using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.WebApi;

namespace UnTaskAlert
{
    public interface IBacklogAccessor
    {
        Task<ActiveTaskInfo> GetActiveWorkItems(VssConnection connection, string name, ILogger log);
        Task<List<int>> GetWorkItemsForPeriod(VssConnection connection, string name, DateTime dateTime, ILogger log);
        Task<IList<WorkItem>> GetWorkItemsById(VssConnection connection, List<int> workItemsIds);
        Task<IList<WorkItemUpdate>> GetWorkItemUpdates(VssConnection connection, int workItemId);
    }
}
