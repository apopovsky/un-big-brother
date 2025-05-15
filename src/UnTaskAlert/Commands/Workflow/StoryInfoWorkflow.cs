using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.WebApi;
using UnTaskAlert.Models;

namespace UnTaskAlert.Commands.Workflow;

public class StoryInfoWorkflow : CommandWorkflow
{
    private IBacklogAccessor _backlogAccessor;
    private VssConnection _vssConnection;

    protected override void InjectDependencies(IServiceScopeFactory serviceScopeFactory)
    {
        var scope = serviceScopeFactory.CreateScope();
        _backlogAccessor = scope.ServiceProvider.GetService<IBacklogAccessor>();
        _vssConnection = scope.ServiceProvider.GetService<VssConnection>();
    }

    protected override bool DoesAccept(string input)
    {
        return input.Trim().StartsWith("/storyinfo", StringComparison.OrdinalIgnoreCase);
    }

    protected override async Task<WorkflowResult> PerformStep(string input, Subscriber subscriber, long chatId)
    {
        var parts = input.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2 || !int.TryParse(parts[1], out var storyId))
        {
            await Notifier.Respond(chatId, "Uso: /storyinfo [id de User Story]");
            return WorkflowResult.Finished;
        }
        await ReportingService.StoryInfoReport(subscriber, Config.AzureDevOpsAddress, Config.AzureDevOpsAccessToken, storyId, Logger);
        return WorkflowResult.Finished;
    }
}
