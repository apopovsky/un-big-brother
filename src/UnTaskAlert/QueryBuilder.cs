namespace UnTaskAlert;

public class QueryBuilder : IQueryBuilder
{
	public string GetActiveWorkItemsQuery(string userName)
	{
		var query = "Select [State], [Title] " +
		            "From WorkItems " +
		            "Where [Work Item Type] = 'Task' " +
		            $"And [Assigned To] = '{userName}' " +
		            "And [State] = 'Active' " +
		            "Order By [State] Asc, [Changed Date] Desc";
		return query;
	}

	public string GetActivePullRequestsQuery(string userEmail)
	{
		// Azure DevOps PR queries are executed via GitHttpClient with search criteria.
		// We still keep a human-readable query string for consistent logging.
		return $"PullRequests: status=Active, createdBy='{userEmail}', scope=all-projects";
	}

	public string GetWorkItemsByDate(string userName, DateTime @from, DateTime? to)
	{
		var dateTo = (to ?? DateTime.UtcNow).Date.AddDays(1);
		var query = $@"SELECT [System.Id],
								    [System.WorkItemType],
								    [System.Title],
								    [System.AssignedTo],
								    [System.State],
								    [System.Tags],
								    [Microsoft.VSTS.Common.ClosedDate],
								    [Microsoft.VSTS.Scheduling.CompletedWork]
								FROM workitems
								WHERE
								    [System.AssignedTo] = '{userName}'
								    AND [System.WorkItemType] = 'Task'
								    AND (
                                        ([Microsoft.VSTS.Common.ClosedDate] >= '{from:yyyy-MM-dd}'
                                        AND [Microsoft.VSTS.Common.ClosedDate] < '{dateTo:yyyy-MM-dd}'
                                        )
								        OR (
								            [Microsoft.VSTS.Scheduling.CompletedWork] > 0
								            AND [System.State] <> 'Closed'
								        )
								        OR ( [System.State] = 'Active')
								    )";

		return query;
	}
}