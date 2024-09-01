using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Telegram.Bot.Types;
using UnTaskAlert.Common;

namespace UnTaskAlert.Functions;

// ReSharper disable once ClassNeverInstantiated.Global
public class TelegramBotFunction(ICommandProcessor commandProcessor)
{
    private readonly ICommandProcessor _commandProcessor = Arg.NotNull(commandProcessor, nameof(commandProcessor));

    [Function(nameof(TelegramBotFunction))]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
        FunctionContext context)
    {
        var requestBody = await new StreamReader(req.Body).ReadToEndAsync(context.CancellationToken);
        var log = context.GetLogger(nameof(TelegramBotFunction));
        log.LogInformation("Incoming request:{NewLine}{RequestBody}", Environment.NewLine, requestBody);

        try
        {
            var update = JsonConvert.DeserializeObject<Update>(requestBody);
            await _commandProcessor.Process(update, log, context.CancellationToken);
        }
        catch (Exception e)
        {
            log.LogError(e, "Error while processing the update.");
            return new ConflictResult();
        }

        return new OkObjectResult("Processed");
    }
}