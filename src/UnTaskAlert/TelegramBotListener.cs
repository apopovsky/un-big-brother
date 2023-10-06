﻿using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types.Enums;
using UnTaskAlert.Common;
using UnTaskAlert.MyNamespace;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;

namespace UnTaskAlert
{
	public class TelegramBotListener : ITelegramBotListener
	{
		private readonly ITelegramBotClient _botClient;
		private readonly ICommandProcessor _commandProcessor;
		private ILogger _logger;
        private readonly IUpdateHandler _handler;

        public TelegramBotListener(ICommandProcessor service, ITelegramBotClient botClient)
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

        [FunctionName("botListener")]
		public async Task Run([TimerTrigger("0 0 */24 * * *", RunOnStartup = true)] TimerInfo myTimer, ILogger log)
		{
            _logger = log;
            _botClient.StartReceiving(_handler);

            await Task.CompletedTask;
        }
	}
}