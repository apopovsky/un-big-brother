using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UnTaskAlert.Common;

namespace UnTaskAlert.Functions
{
    public class UnTaskAlertFunction
    {
        private readonly IMonitoringService _service;
        private readonly Config _config;
        private readonly IDbAccessor _dbAccessor;

        public UnTaskAlertFunction(IMonitoringService service, IOptions<Config> options, IDbAccessor dbAccessor)
        {
            _service = Arg.NotNull(service, nameof(service));
            _config = Arg.NotNull(options.Value, nameof(options));
            _dbAccessor = Arg.NotNull(dbAccessor, nameof(dbAccessor));
        }

        [Function(nameof(UnTaskAlertFunction))]
        public async Task Run([TimerTrigger("0 */10 * * * *")] TimerInfo myTimer, FunctionContext context)
        {
            var logger = context.GetLogger(nameof(UnTaskAlertFunction));
            logger.LogInformation("Executing monitoring task");

            var subscribers = await _dbAccessor.GetSubscribers();
            foreach (var subscriber in subscribers)
            {
                try
                {
                    await _service.PerformMonitoring(subscriber,
                        _config.AzureDevOpsAddress,
                        _config.AzureDevOpsAccessToken,
                        logger, context.CancellationToken);
                }
                catch (Exception e)
                {
                    logger.LogError(e, e.Message);
                }
            }
        }
    }
}
