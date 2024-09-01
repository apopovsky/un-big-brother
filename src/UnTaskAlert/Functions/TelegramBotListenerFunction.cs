using System.Diagnostics;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using UnTaskAlert.Common;
using UnTaskAlert.MyNamespace;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;

namespace UnTaskAlert.Functions
{
    public class TelegramBotListenerFunction : ITelegramBotListener
    {
        private readonly ITelegramBotClient _botClient;
        private readonly IServiceProvider _serviceProvider;
        private readonly ICommandProcessor _commandProcessor;
        private ILogger _logger;
        private readonly IUpdateHandler _handler;

        public TelegramBotListenerFunction(ICommandProcessor service, ITelegramBotClient botClient)
        {
            _botClient = botClient;
            _commandProcessor = Arg.NotNull(service, nameof(service));
            _handler = new DefaultUpdateHandler(HandleUpdateAsync, HandleErrorAsync);
        }

        private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            if (update.Type == UpdateType.Message)
            {
                try
                {
                    _logger.LogInformation($"Message received from: {update.Message.From}. Message: {update.Message.Text}");
                    await _commandProcessor.Process(update, _logger, cancellationToken);
                }
                catch (Exception exception)
                {
                    Debug.WriteLine(exception.ToString());
                    _logger.LogError(new EventId(), exception, exception.Message);
                    try
                    {
                        await _botClient.SendTextMessageAsync(update.Message.Chat.Id, "Could not process your request", cancellationToken: cancellationToken);
                    }
                    catch (Exception e)
                    {
                        Debug.WriteLine(e.ToString());
                        _logger.LogError(new EventId(), e, e.Message);
                    }
                }
            }
        }

        private Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception,
            CancellationToken cancellationToken)
        {
            _logger.LogError(new EventId(), exception, exception?.Message);
            return Task.CompletedTask;
        }

        [Function(nameof(TelegramBotListenerFunction))]
        public async Task Run([TimerTrigger("0 0 */24 * * *", RunOnStartup = true)] TimerInfo myTimer, FunctionContext context)
        {
            _logger = context.GetLogger(nameof(TelegramBotListenerFunction));
            _botClient.StartReceiving(_handler, cancellationToken: context.CancellationToken);

            await Task.CompletedTask;
        }
    }
}