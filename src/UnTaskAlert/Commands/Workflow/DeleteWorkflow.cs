using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UnTaskAlert.Common;
using UnTaskAlert.Models;

namespace UnTaskAlert.Commands.Workflow
{
    public class DeleteWorkflow : CommandWorkflow
    {
        private IDbAccessor _dbAccessor;

        enum Steps
        {
            Confirm = 0,
            Delete = 1
        }

        protected override async Task<WorkflowResult> PerformStep(string input, Subscriber subscriber, long chatId)
        {
            if (CurrentStep == (int)Steps.Confirm)
            {
                await Notifier.Respond(chatId, "Are you sure? (Y/N)");
                return WorkflowResult.Continue;
            }

            if (input.Equals("y", StringComparison.OrdinalIgnoreCase) ||
                input.Equals("yes", StringComparison.OrdinalIgnoreCase))
            {
                Logger.LogInformation($"Deleting subscriber '{subscriber.TelegramId}'");
                await _dbAccessor.DeleteIfExists(subscriber);
            }

            return WorkflowResult.Finished;
        }

        protected override void InjectDependencies(IServiceProvider serviceProvider)
        {
            _dbAccessor = new DbAccessor(serviceProvider, serviceProvider.GetService<IOptions<Config>>());
        }

        protected override bool DoesAccept(string input)
        {
            return input.StartsWith("/delete", StringComparison.OrdinalIgnoreCase);
        }
    }
}