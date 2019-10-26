using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using UnTaskAlert.Common;
using UnTaskAlert.Models;

namespace UnTaskAlert
{
	public class UnTaskReportFunction
	{
		private readonly IReportingService _service;
		private readonly Config _config;

		public UnTaskReportFunction(IReportingService service, IOptions<Config> options)
		{
			_service = Arg.NotNull(service, nameof(service));
			_config = Arg.NotNull(options.Value, nameof(options));
		}

		[FunctionName("UnTaskReportFunction")]
		//public async Task Run([TimerTrigger("0 */10 * * * *")]TimerInfo myTimer)
		public async Task Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = null)] HttpRequest req, ILogger log)
		{
			log.LogInformation($"Executing monitoring task");
			log.LogInformation($"Reading subscribers: '{_config.Subscribers}'");

			var subscribers = JsonConvert.DeserializeObject<Subscribers>(_config.Subscribers);
			foreach (var subscriber in subscribers.Items)
			{
				try
				{
					await _service.CreateReport(subscriber,
						_config.AzureDevOpsAddress,
						_config.AzureDevOpsAccessToken, log);
				}
				catch (Exception e)
				{
					log.LogError(e.ToString());
				}
			}
		}
	}
}