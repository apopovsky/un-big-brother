using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.WebApi;
using UnTaskAlert.Models;

namespace UnTaskAlert;

public class BacklogAccessor(IQueryBuilder queryBuilder) : IBacklogAccessor
{
    public async Task<ActiveTasksInfo> GetActiveWorkItems(VssConnection connection, string name, ILogger log)
    {
        var query = queryBuilder.GetActiveWorkItemsQuery(name);

        var wiql = new Wiql { Query = query };

        var client = connection.GetClient<WorkItemTrackingHttpClient>();
        try
        {
            log.LogInformation("Executing query {Query}", query);
            var queryResult = await client.QueryByWiqlAsync(wiql);

            var result = new ActiveTasksInfo
            {
                ActiveTaskCount = queryResult.WorkItems.Count(),
                User = name,
                TasksInfo = queryResult.WorkItems.Select(i => new TaskInfo { Id = i.Id }).ToList(),
            };
            log.LogInformation("Query Result: HasActiveTask is '{HasActiveTasks}', ActiveTaskCount is '{ActiveTaskCount}'", result.HasActiveTasks, result.ActiveTaskCount);

            return result;
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Exception occurred while executing query {Query}", query);
            throw;
        }
    }

    public async Task<List<int>> GetWorkItemsForPeriod(VssConnection connection, string userName, DateTime dateTime,
        DateTime? dateTo, ILogger log)
    {
        var query = queryBuilder.GetWorkItemsByDate(userName, dateTime, dateTo);

        var wiql = new Wiql { Query = query };

        var client = connection.GetClient<WorkItemTrackingHttpClient>();
        try
        {
            log.LogInformation("Executing query {Query}", query);
            var result = await client.QueryByWiqlAsync(wiql, timePrecision: true);
            return result.WorkItems.Select(w => w.Id).ToList();
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Exception occurred while executing query {Query}", query);
            throw;
        }
    }

    public async Task<IList<WorkItem>> GetWorkItemsById(VssConnection connection, List<int> workItemsIds)
    {
        if (!workItemsIds.Any())
        {
            return Array.Empty<WorkItem>();
        }

        var client = connection.GetClient<WorkItemTrackingHttpClient>();
        return await client.GetWorkItemsAsync(workItemsIds, expand: WorkItemExpand.Fields);
    }

    public async Task<IList<WorkItemUpdate>> GetWorkItemUpdates(VssConnection connection, int workItemId)
    {
        var client = connection.GetClient<WorkItemTrackingHttpClient>();
        return await client.GetUpdatesAsync(workItemId);
    }

    public async Task<TimeSpan> GetWorkItemActiveTime(VssConnection connection, int workItemId)
    {
        var updates = await GetWorkItemUpdates(connection, workItemId);
        DateTime? activeStart = null;
        var activeTime = TimeSpan.Zero;
        foreach (var itemUpdate in updates)
        {
            if (itemUpdate.Fields == null || !itemUpdate.Fields.TryGetValue("System.State", out var updateField)) continue;
            if (updateField.NewValue.ToString() == "Active")
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

        //Add running active time to current active task
        if (activeStart.HasValue)
        {
            var span = DateTime.UtcNow - activeStart;
            activeTime = activeTime.Add(span.GetValueOrDefault());
        }

        return activeTime;
    }

}