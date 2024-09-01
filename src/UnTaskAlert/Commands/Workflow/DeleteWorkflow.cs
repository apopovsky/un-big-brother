using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UnTaskAlert.Models;

namespace UnTaskAlert.Commands.Workflow
{
    public class DeleteWorkflow() : CommandWorkflow
    {
        private IDbAccessor _dbAccessor;

        private enum Steps
        {
            Confirm = 0,
            Delete = 1,
        }

        protected override async Task<WorkflowResult> PerformStep(string input, Subscriber subscriber, long chatId)
        {
            if (CurrentStep == (int)Steps.Confirm)
            {
                await Notifier.Respond(chatId, "Are you sure? (Y/N)");
                return WorkflowResult.Continue;
            }

            if (!input.Equals("y", StringComparison.OrdinalIgnoreCase) &&
                !input.Equals("yes", StringComparison.OrdinalIgnoreCase)) return WorkflowResult.Finished;
            
            Logger.LogInformation("Deleting subscriber '{id}'", subscriber.TelegramId);
            await _dbAccessor.DeleteIfExists(subscriber);

            return WorkflowResult.Finished;
        }

        protected override void InjectDependencies(IServiceScopeFactory serviceScopeFactory)
        {
            var options = serviceScopeFactory.CreateScope().ServiceProvider.GetService<IOptions<Config>>();
            _dbAccessor = new DbAccessor(serviceScopeFactory, options);
        }

        protected override bool DoesAccept(string input) => input.StartsWith("/delete", StringComparison.OrdinalIgnoreCase);
    }
}