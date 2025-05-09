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

        var client = await connection.GetClientAsync<WorkItemTrackingHttpClient>();
        try
        {
            log.LogInformation("Executing query {Query}", query);
            var queryResult = await client.QueryByWiqlAsync(wiql);
            IList<WorkItem> tasks = new List<WorkItem>();
            if (queryResult.WorkItems.Any())
            {
                tasks = await GetWorkItemsById(connection, queryResult.WorkItems.Select(x => x.Id).ToList());
            }

            var result = new ActiveTasksInfo
            {
                ActiveTaskCount = queryResult.WorkItems.Count(),
                User = name,
                TasksInfo = queryResult.WorkItems.Select(i => new TaskInfo
                { Id = i.Id, Title = tasks.First(x => x.Id == i.Id).Fields["System.Title"].ToString() }).ToList(),
            };
            log.LogInformation("Query Result: HasActiveTask is '{HasActiveTasks}', ActiveTaskCount is '{ActiveTaskCount}'", result.HasActiveTasks, result.ActiveTaskCount);

            return result;
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Exception occurred while executing query {Query} for user {User}", query, name);
            throw new InvalidOperationException($"Error executing query for user {name}", ex);
        }
    }

    public async Task<List<int>> GetWorkItemsForPeriod(VssConnection connection, string username, DateTime dateTime,
        DateTime? dateTo, ILogger log)
    {
        var query = queryBuilder.GetWorkItemsByDate(username, dateTime, dateTo);

        var wiql = new Wiql { Query = query };

        var client = await connection.GetClientAsync<WorkItemTrackingHttpClient>();
        try
        {
            log.LogInformation("Executing query {Query}", query);
            var result = await client.QueryByWiqlAsync(wiql, timePrecision: true);
            return result.WorkItems.Select(w => w.Id).ToList();
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Exception occurred while executing query {Query} for user {UserName}", query, username);
            throw new InvalidOperationException($"Error executing query for user {username}", ex);
        }
    }

    public async Task<IList<WorkItem>> GetWorkItemsById(VssConnection connection, List<int> workItemsIds)
    {
        if (!workItemsIds.Any())
        {
            return Array.Empty<WorkItem>();
        }

        var client = await connection.GetClientAsync<WorkItemTrackingHttpClient>();
        return await client.GetWorkItemsAsync(workItemsIds, expand: WorkItemExpand.Fields);
    }

    public async Task<IList<WorkItemUpdate>> GetWorkItemUpdates(VssConnection connection, int workItemId)
    {
        var client = await connection.GetClientAsync<WorkItemTrackingHttpClient>();
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
        public async Task<WorkItem> GetParentUserStory(VssConnection connection, int workItemId)
    {
        var client = await connection.GetClientAsync<WorkItemTrackingHttpClient>();
        WorkItem workItem;
        try
        {
            workItem = await client.GetWorkItemAsync(workItemId, expand: WorkItemExpand.Relations);
        }
        catch
        {
            // If the work item does not exist or cannot be retrieved, return null
            return null;
        }

        var parentRelation = workItem.Relations?.FirstOrDefault(r => r.Rel == "System.LinkTypes.Hierarchy-Reverse");
        if (parentRelation == null)
        {
            return null;
        }

        int parentId;
        if (!int.TryParse(parentRelation.Url.Split('/').Last(), out parentId))
        {
            // If the parentId cannot be parsed, return null
            return null;
        }

        try
        {
            return await client.GetWorkItemAsync(parentId);
        }
        catch
        {
            // If the parent work item does not exist or cannot be retrieved, return null
            return null;
        }
    }
}
