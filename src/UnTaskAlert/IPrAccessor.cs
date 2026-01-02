using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Services.WebApi;
using UnTaskAlert.Models;

namespace UnTaskAlert;

public interface IPrAccessor
{
    Task<ActivePullRequestsInfo> GetActivePullRequests(VssConnection connection, string azureDevOpsAddress, string userEmail, IEnumerable<string> projectNames, ILogger log);
}