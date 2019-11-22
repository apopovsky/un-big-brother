using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using UnTaskAlert.Common;
using UnTaskAlert.Models;

namespace UnTaskAlert.Commands.Workflow
{
    public abstract class CommandWorkflow
    {
        protected CommandWorkflow()
        {
            Expiration = DateTime.UtcNow.AddMinutes(5);
        }

        private bool _isInitialized;

        private static readonly int PauseBeforeAnswer = 1000;

        protected IServiceProvider ServiceProvider { get; set; }
        protected ILogger Logger { get; set; }
        protected INotifier Notifier { get; set; }
        protected IDbAccessor DbAccessor { get; set; }
        protected IReportingService ReportingService { get; set; }
        protected Config Config { get; set; }

        [JsonIgnore]
        public virtual bool IsVerificationRequired { get; set; } = true;
        [JsonIgnore]
        public bool IsExpired => DateTime.UtcNow > Expiration;
        public DateTime Expiration { get; set; }
        public int CurrentStep { get; set; }

        public void Inject(IServiceProvider serviceProvider, Config config, ILogger logger)
        {
            Logger = Arg.NotNull(logger, nameof(logger));
            Config = Arg.NotNull(config, nameof(config));
            ServiceProvider = Arg.NotNull(serviceProvider, nameof(serviceProvider));
            Notifier = Arg.NotNull(serviceProvider.GetService<INotifier>(), $"Could not resolve '{nameof(INotifier)}'");
            DbAccessor = Arg.NotNull(serviceProvider.GetService<IDbAccessor>(), $"Could not resolve '{nameof(IDbAccessor)}'");
            ReportingService = Arg.NotNull(ServiceProvider.GetService<IReportingService>(), $"Could not resolve '{nameof(IReportingService)}'");

            _isInitialized = true;
        }

        public async Task<WorkflowResult> Step(string input, Subscriber subscriber, long chatId)
        {
            Logger.LogInformation($"Executing workflow {this.GetType()} for subscriber '{subscriber.TelegramId}'");

            if (!_isInitialized)
            {
                throw new InvalidOperationException("Workflow is not initialized. Call 'Inject' first.");
            }

            if (IsVerificationRequired && !subscriber.IsVerified)
            {
                Logger.LogInformation($"Command '{input} is available only for verified users'");
                await Notifier.Respond(chatId, "Verification is required");
                return WorkflowResult.Finished;
            }

            if (!IsVerificationRequired && !subscriber.IsVerified)
            {
                // making a pause for security reasons
                await Task.Delay(PauseBeforeAnswer);
            }

            if (input.Equals("/cancel", StringComparison.OrdinalIgnoreCase))
            {
                await Notifier.Respond(chatId, $"Command cancelled");
                return WorkflowResult.Finished;
            }

            return await PerformStep(input, subscriber, chatId);
        }

        public bool Accepts(string input)
        {
            if (input.Equals("/exit", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return DoesAccept(input);
        }

        //TODO: an evil todo. This could be a serialized object of different classes using the type name stored by json.net
        public string Data { get; set; }

        protected abstract bool DoesAccept(string input);
        protected abstract Task<WorkflowResult> PerformStep(string input, Subscriber subscriber, long chatId);
    }

    public enum WorkflowResult
    {
        Continue,
        Finished
    }
}