using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Telegram.Bot.Types;
using UnTaskAlert.Common;

namespace UnTaskAlert
{
    public class TelegramBotFunction
    {
        private readonly ICommandProcessor _commandProcessor;

        public TelegramBotFunction(ICommandProcessor commandProcessor)
        {
            _commandProcessor = Arg.NotNull(commandProcessor, nameof(commandProcessor));
        }

        [FunctionName("bot")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            log.LogInformation($"Incoming request:{Environment.NewLine}{requestBody}");

            try
            {
                var update = JsonConvert.DeserializeObject<Update>(requestBody);
                await _commandProcessor.Process(update, log);
            }
            catch (Exception e)
            {
                log.LogError(e.ToString());
            }

            return new OkObjectResult("Processed");
        }
    }
}
