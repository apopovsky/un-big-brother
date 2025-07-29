using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using UnTaskAlert.Common;

namespace UnTaskAlert.Functions;

public class TelegramBotBackgroundService : BackgroundService
{
    private readonly ITelegramBotClient _botClient;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<TelegramBotBackgroundService> _logger;
    private readonly IUpdateHandler _handler;

    public TelegramBotBackgroundService(ITelegramBotClient botClient, IServiceScopeFactory serviceScopeFactory, ILogger<TelegramBotBackgroundService> logger)
    {
        _botClient = botClient;
        _serviceScopeFactory = Arg.NotNull(serviceScopeFactory, nameof(serviceScopeFactory));
        _logger = logger;
        _handler = new DefaultUpdateHandler(HandleUpdateAsync, HandleErrorAsync);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("Starting Telegram bot receiving loop.");
                _botClient.StartReceiving(_handler, cancellationToken: stoppingToken);
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Telegram bot listener crashed. Restarting in 5 seconds...");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        if (update.Type == UpdateType.Message)
        {
            try
            {
                _logger.LogInformation("Message received from: {From}. Message: {Text}", update.Message?.From, update.Message?.Text);
                
                // Crear un scope para resolver dependencias scoped
                using var scope = _serviceScopeFactory.CreateScope();
                var commandProcessor = scope.ServiceProvider.GetRequiredService<ICommandProcessor>();
                
                await commandProcessor.Process(update, _logger, cancellationToken);
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
}
