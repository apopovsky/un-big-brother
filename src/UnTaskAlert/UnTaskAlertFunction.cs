using System;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using UnTaskAlert.Common;
using UnTaskAlert.Models;

namespace UnTaskAlert
{
    public class UnTaskAlertFunction
    {
        private readonly IMonitoringService _service;
        private readonly Config _config;

        public UnTaskAlertFunction(IMonitoringService service, IOptions<Config> options)
        {
            _service = Arg.NotNull(service, nameof(service));
            _config = Arg.NotNull(options.Value, nameof(options));
        }

        [FunctionName("activeTaskMonitoring")]
        public async Task Run([TimerTrigger("0 */20 * * * *")]TimerInfo myTimer, ILogger log)
        {
            log.LogInformation($"Executing monitoring task");
            log.LogInformation($"Reading subscribers: '{_config.Subscribers}'");

            var subscribers = JsonConvert.DeserializeObject<Subscribers>(_config.Subscribers);
            foreach (var subscriber in subscribers.Items)
            {
                try
                {
                    await _service.PerformMonitoring(subscriber,
                        _config.AzureDevOpsAddress,
                        _config.AzureDevOpsAccessToken,
                        log);
                }
                catch (Exception e)
                {
                    log.LogError(e.ToString());
                }
            }
        }
    }
}
