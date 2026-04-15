using System;

namespace UnTaskAlert.Tests;

[TestClass]
public sealed class QueryBuilderTests
{
    [TestMethod]
    public void GetActiveWorkItemsQuery_IncludesUsernameAndActiveState()
    {
        var builder = new QueryBuilder();
        var query = builder.GetActiveWorkItemsQuery("Alice");

        StringAssert.Contains(query, "[Assigned To] = 'Alice'");
        StringAssert.Contains(query, "[State] = 'Active'");
        StringAssert.Contains(query, "Where [Work Item Type] = 'Task'");
    }

    [TestMethod]
    public void GetActivePullRequestsQuery_ContainsCreatedByEmail()
    {
        var builder = new QueryBuilder();
        var query = builder.GetActivePullRequestsQuery("alice@contoso.com");

        StringAssert.Contains(query, "createdBy='alice@contoso.com'");
        StringAssert.Contains(query, "status=Active");
    }

    [TestMethod]
    public void GetWorkItemsByDate_IncludesFromAndToRange()
    {
        var builder = new QueryBuilder();

        var from = new DateTime(2025, 01, 15, 10, 30, 00, DateTimeKind.Utc);
        var to = new DateTime(2025, 01, 20, 05, 00, 00, DateTimeKind.Utc);
        var query = builder.GetWorkItemsByDate("Alice", from, to);

        StringAssert.Contains(query, "[System.AssignedTo] = 'Alice'");
        StringAssert.Contains(query, "ClosedDate] >= '2025-01-15'");
        StringAssert.Contains(query, "ClosedDate] < '2025-01-21'");
    }
}
