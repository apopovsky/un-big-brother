using System;
using System.Threading;
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

        private static readonly int PauseBeforeAnswer = 2000;

        protected ILogger Logger { get; set; }
        protected INotifier Notifier { get; set; }
        protected IReportingService ReportingService { get; set; }
        protected Config Config { get; set; }

        [JsonIgnore]
        public virtual bool IsVerificationRequired => true;
        [JsonIgnore]
        public bool IsExpired => DateTime.UtcNow > Expiration;
        public DateTime Expiration { get; set; }
        public int CurrentStep { get; set; }

        public void Inject(IServiceProvider serviceProvider, Config config, ILogger logger)
        {
            Logger = Arg.NotNull(logger, nameof(logger));
            Config = Arg.NotNull(config, nameof(config));
            Notifier = Arg.NotNull(serviceProvider.GetService<INotifier>(), $"Could not resolve '{nameof(INotifier)}'");
            ReportingService = Arg.NotNull(serviceProvider.GetService<IReportingService>(), $"Could not resolve '{nameof(IReportingService)}'");

            InjectDependencies(serviceProvider);

            _isInitialized = true;
        }

        protected abstract void InjectDependencies(IServiceProvider serviceProvider);

        public async Task<WorkflowResult> Step(string input, Subscriber subscriber, long chatId, CancellationToken cancellationToken)
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
                Logger.LogInformation($"Pause responding for {PauseBeforeAnswer} ms");
                await Task.Delay(PauseBeforeAnswer);
            }

            if (input.Equals("/cancel", StringComparison.OrdinalIgnoreCase))
            {
                await Notifier.Respond(chatId, $"Command cancelled");
                return WorkflowResult.Finished;
            }

            try
            {
                await Notifier.Typing(chatId.ToString(), cancellationToken);
                return await PerformStep(input, subscriber, chatId);
            }
            catch (Exception e)
            {
                Logger.LogError(e.ToString());
                await Notifier.Respond(chatId, "Something bad happened to the bot :(");
                throw;
            }
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