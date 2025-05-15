using System.Diagnostics;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using UnTaskAlert.Common;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;

namespace UnTaskAlert.Functions;

public class TelegramBotListenerFunction : ITelegramBotListener
{
    private readonly ITelegramBotClient _botClient;
    private readonly ICommandProcessor _commandProcessor;
    private ILogger _logger;
    private readonly IUpdateHandler _handler;

    public TelegramBotListenerFunction(ICommandProcessor commandProcessor, ITelegramBotClient botClient)
    {
        _botClient = botClient;
        _commandProcessor = Arg.NotNull(commandProcessor, nameof(commandProcessor));
        _handler = new DefaultUpdateHandler(HandleUpdateAsync, HandleErrorAsync);
    }

    private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        if (update.Type == UpdateType.Message)
        {
            try
            {
                _logger.LogInformation("Message received from: {From}. Message: {Text}", update.Message?.From, update.Message?.Text);
                await _commandProcessor.Process(update, _logger, cancellationToken);
            }
            catch (Exception exception)
            {
                Debug.WriteLine(exception.ToString());
                _logger.LogError(exception, "Error processing the update.");
                try
                {
                    await _botClient.SendMessage(update.Message!.Chat.Id, "Could not process your request", cancellationToken: cancellationToken);
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e.ToString());
                    _logger.LogError(e, "Error processing the bot request.");
                }
            }
        }
    }

    private Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "Error processing");
        return Task.CompletedTask;
    }

    [Function(nameof(TelegramBotListenerFunction))]
    // ReSharper disable once UnusedParameter.Global
    public async Task Run([TimerTrigger("0 0 */24 * * *", RunOnStartup = true)] TimerInfo timerInfo, FunctionContext context)
    {
        _logger = context.GetLogger(nameof(TelegramBotListenerFunction));
        _botClient.StartReceiving(_handler, cancellationToken: context.CancellationToken);

        await Task.CompletedTask;
    }
}