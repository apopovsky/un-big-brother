namespace UnTaskAlert;

public interface IQueryBuilder
{
	string GetActiveWorkItemsQuery(string userName);
	string GetWorkItemsByDate(string userName, DateTime @from, DateTime? to);
}