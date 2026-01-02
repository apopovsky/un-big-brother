namespace UnTaskAlert.Models;

public class ActivePullRequestsInfo
{
    public string User { get; set; }
    public bool HasActivePullRequests => ActivePullRequestCount > 0;
    public int ActivePullRequestCount { get; set; }
    public List<PullRequestInfo> PullRequests { get; set; }
}

public class PullRequestInfo
{
    public int Id { get; set; }
    public string Title { get; set; }
    public string Project { get; set; }
    public string Repository { get; set; }
    public string WebUrl { get; set; }
    public DateTime CreationDateUtc { get; set; }
}