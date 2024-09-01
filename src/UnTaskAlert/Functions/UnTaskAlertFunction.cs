using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UnTaskAlert.Common;

namespace UnTaskAlert.Functions;

// ReSharper disable once ClassNeverInstantiated.Global
public class UnTaskAlertFunction(IMonitoringService service, IOptions<Config> options, IDbAccessor dbAccessor)
{
    private readonly IMonitoringService _service = Arg.NotNull(service, nameof(service));
    private readonly Config _config = Arg.NotNull(options.Value, nameof(options));
    private readonly IDbAccessor _dbAccessor = Arg.NotNull(dbAccessor, nameof(dbAccessor));

    [Function(nameof(UnTaskAlertFunction))]
    // ReSharper disable once UnusedParameter.Global
    public async Task Run([TimerTrigger("0 */10 * * * *")] TimerInfo timerInfo, FunctionContext context)
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
                logger.LogError(e, "An error occurred while performing monitoring.");
            }
        }
    }
}