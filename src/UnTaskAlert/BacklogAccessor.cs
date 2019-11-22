using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.WebApi;
using UnTaskAlert.Models;

namespace UnTaskAlert
{
    public class BacklogAccessor : IBacklogAccessor
    {
        private readonly IQueryBuilder _queryBuilder;

        public BacklogAccessor(IQueryBuilder queryBuilder)
        {
            _queryBuilder = queryBuilder;
        }

        public async Task<ActiveTaskInfo> GetActiveWorkItems(VssConnection connection, string name, ILogger log)
        {
            var query = _queryBuilder.GetActiveWorkItemsQuery(name);

            var wiql = new Wiql { Query = query };

            var client = connection.GetClient<WorkItemTrackingHttpClient>();
            try
            {
                log.LogInformation($"Executing query {query}");
                var queryResult = await client.QueryByWiqlAsync(wiql);

                var result = new ActiveTaskInfo
                {
                    ActiveTaskCount = queryResult.WorkItems.Count(),
                    User = name,
                    WorkItemsIds = queryResult.WorkItems.Select(i => i.Id).ToList()
                };
                log.LogInformation($"Query Result: HasActiveTask is '{result.HasActiveTasks}', ActiveTaskCount is '{result.ActiveTaskCount}'");

                return result;
            }
            catch (Exception ex)
            {
                log.LogError($"Exception occured {ex}");
                throw;
            }
        }

        public async Task<List<int>> GetWorkItemsForPeriod(VssConnection connection, string userName, DateTime dateTime, ILogger log)
        {
            var query = _queryBuilder.GetWorkItemsByDate(userName, dateTime, null);

            var wiql = new Wiql { Query = query };

            var client = connection.GetClient<WorkItemTrackingHttpClient>();
            try
            {
                log.LogInformation($"Executing query {query}");
                var result = await client.QueryByWiqlAsync(wiql, timePrecision:true);
                return result.WorkItems.Select(w=>w.Id).ToList();
            }
            catch (Exception ex)
            {
                log.LogError($"Exception occured {ex}");
                throw;
            }
        }

        public async Task<IList<WorkItem>> GetWorkItemsById(VssConnection connection, List<int> workItemsIds)
        {
            if (!workItemsIds.Any())
            {
                return new List<WorkItem>();
            }

            var client = connection.GetClient<WorkItemTrackingHttpClient>();
            return await client.GetWorkItemsAsync(workItemsIds, expand: WorkItemExpand.Fields);
        }

        public async Task<IList<WorkItemUpdate>> GetWorkItemUpdates(VssConnection connection, int workItemId)
        {
            var client = connection.GetClient<WorkItemTrackingHttpClient>();
            return await client.GetUpdatesAsync(workItemId);
        }
    }
}
