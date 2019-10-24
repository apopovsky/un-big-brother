using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.WebApi;

namespace UnTaskAlert
{
    public class BacklogAccessor : IBacklogAccessor
    {
        public async Task<ActiveTaskInfo> GetActiveWorkItems(VssConnection connection, string name, ILogger log)
        {
            var query = "Select [State], [Title] " +
                        "From WorkItems " +
                        "Where [Work Item Type] = 'Task' " +
                        $"And [Assigned To] = '{name}' " +
                        "And [State] = 'Active' " +
                        "Order By [State] Asc, [Changed Date] Desc";

            var wiql = new Wiql { Query = query };

            var client = connection.GetClient<WorkItemTrackingHttpClient>();
            try
            {
                log.LogInformation($"Executing query {query}");
                var queryResult = await client.QueryByWiqlAsync(wiql);

                var result = new ActiveTaskInfo
                {
                    ActiveTaskCount = queryResult.WorkItems.Count(),
                    User = name
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
    }
}
