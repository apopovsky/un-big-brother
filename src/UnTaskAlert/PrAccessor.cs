using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using UnTaskAlert.Models;

namespace UnTaskAlert;

public class PrAccessor(IQueryBuilder queryBuilder) : IPrAccessor
{
    public async Task<ActivePullRequestsInfo> GetActivePullRequests(
        VssConnection connection,
        string azureDevOpsAddress,
        string userEmail,
        IEnumerable<string> projectNames,
        ILogger log)
    {
        var query = queryBuilder.GetActivePullRequestsQuery(userEmail);

        try
        {
            log.LogInformation("Executing query {Query}", query);

            var gitClient = await connection.GetClientAsync<GitHttpClient>();

            var projects = (projectNames ?? Array.Empty<string>())
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => p.Trim())
                .ToList();

            var criteria = new GitPullRequestSearchCriteria
            {
                Status = PullRequestStatus.Active,
            };

            var prs = new List<GitPullRequest>();
            if (projects.Count == 0)
            {
                prs.AddRange(await gitClient.GetPullRequestsByProjectAsync(project: null, searchCriteria: criteria));
            }
            else
            {
                foreach (var project in projects)
                {
                    prs.AddRange(await gitClient.GetPullRequestsByProjectAsync(project: project, searchCriteria: criteria));
                }
            }

            var pullRequests = prs
                .Where(pr => string.Equals(pr.CreatedBy?.UniqueName, userEmail, StringComparison.OrdinalIgnoreCase))
                .Select(pr =>
                {
                    var projectName = pr.Repository?.ProjectReference?.Name;
                    var repoName = pr.Repository?.Name;

                    return new PullRequestInfo
                    {
                        Id = pr.PullRequestId,
                        Title = pr.Title,
                        Project = projectName,
                        Repository = repoName,
                        WebUrl = TryGetWebUrl(pr) ?? BuildWebUrl(azureDevOpsAddress, projectName, repoName, pr.PullRequestId),
                        CreationDateUtc = pr.CreationDate.ToUniversalTime(),
                    };
                })
                .OrderByDescending(p => p.CreationDateUtc)
                .ToList();

            var result = new ActivePullRequestsInfo
            {
                ActivePullRequestCount = pullRequests.Count,
                User = userEmail,
                PullRequests = pullRequests,
            };

            log.LogInformation(
                "Query Result: HasActivePullRequests is '{HasActivePullRequests}', ActivePullRequestCount is '{ActivePullRequestCount}'",
                result.HasActivePullRequests,
                result.ActivePullRequestCount);

            return result;
        }
        catch (VssUnauthorizedException ex)
        {
            log.LogError(ex, "Unauthorized while executing query {Query} for user {User}", query, userEmail);
            throw new InvalidOperationException(
                "Azure DevOps authorization failed while querying pull requests. Ensure the PAT has 'Code (Read)' scope and access to the target repositories.",
                ex);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Exception occurred while executing query {Query} for user {User}", query, userEmail);
            throw new InvalidOperationException($"Error executing pull request query for user {userEmail}", ex);
        }
    }

    private static string TryGetWebUrl(GitPullRequest pr)
    {
        try
        {
            var links = pr.Links?.Links;
            if (links == null)
            {
                return null;
            }

            if (!links.TryGetValue("web", out var webLinkObj) || webLinkObj == null)
            {
                return null;
            }

            var hrefProp = webLinkObj.GetType().GetProperty("Href");
            return hrefProp?.GetValue(webLinkObj)?.ToString();
        }
        catch
        {
            return null;
        }
    }

    private static string BuildWebUrl(string azureDevOpsAddress, string projectName, string repositoryName, int pullRequestId)
    {
        if (string.IsNullOrWhiteSpace(azureDevOpsAddress) || string.IsNullOrWhiteSpace(projectName) || string.IsNullOrWhiteSpace(repositoryName))
        {
            return null;
        }

        var uri = new Uri(azureDevOpsAddress);

        string orgBase;
        if (uri.Host.Equals("dev.azure.com", StringComparison.OrdinalIgnoreCase))
        {
            var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var org = segments.Length >= 1 ? segments[0] : null;
            orgBase = org == null ? null : $"{uri.Scheme}://{uri.Host}/{org}";
        }
        else
        {
            orgBase = $"{uri.Scheme}://{uri.Host}";
        }

        if (string.IsNullOrWhiteSpace(orgBase))
        {
            return null;
        }

        return $"{orgBase}/{projectName}/_git/{repositoryName}/pullrequest/{pullRequestId}";
    }
}