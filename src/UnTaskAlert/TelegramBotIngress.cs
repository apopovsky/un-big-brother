using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Telegram.Bot;
using Telegram.Bot.Types;
using UnTaskAlert.Common;

namespace UnTaskAlert
{
    public class TelegramBotIngress
    {
        private readonly Config _config;
        private readonly ICommandProcessor _commandProcessor;

        public TelegramBotIngress(IOptions<Config> options, ICommandProcessor commandProcessor)
        {
            Arg.NotNull(options, nameof(options));
            _config = options.Value;
            _commandProcessor = Arg.NotNull(commandProcessor, nameof(commandProcessor));
        }

        [FunctionName("bot")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            log.LogInformation($"Incoming request: {requestBody}");
            
            var update = JsonConvert.DeserializeObject<Update>(requestBody);
            await _commandProcessor.Process(update, log);

            return (ActionResult) new OkObjectResult(requestBody);
        }
    }
}
