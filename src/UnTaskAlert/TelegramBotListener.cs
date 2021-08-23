using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Types.Enums;
using UnTaskAlert.Common;
using UnTaskAlert.MyNamespace;

namespace UnTaskAlert
{
	public class TelegramBotListener : ITelegramBotListener
	{
		private readonly ITelegramBotClient _botClient;
		private readonly ICommandProcessor _commandProcessor;
		private ILogger _logger;

        public TelegramBotListener(ICommandProcessor service, ITelegramBotClient botClient)
		{
			_botClient = botClient;
			_commandProcessor = Arg.NotNull(service, nameof(service));
		}

		public void OnUpdateReceived(object sender, UpdateEventArgs updateEventArgs)
		{
			try
            {
                if (updateEventArgs.Update.Type != UpdateType.Message) return;

                _logger.LogInformation($"Message received from: {updateEventArgs.Update.Message.From}. Message: {updateEventArgs.Update.Message.Text}");
                Task.Run(() => _commandProcessor.Process(updateEventArgs.Update, _logger).GetAwaiter().GetResult())
                    .ContinueWith((task) => {
                        var exception = task.Exception;
                        _logger.LogError(new EventId(), exception, exception.Message);
                        _botClient.SendTextMessageAsync(updateEventArgs.Update.Message.Chat.Id, "Could not process your request");

                    }, TaskContinuationOptions.OnlyOnFaulted);
            }
			catch (Exception exception)
			{
				Debug.WriteLine(exception.ToString());
				_logger.LogError(exception, "Error processing message " + updateEventArgs.Update.Id);
			}
		}

		[FunctionName("botListener")]
		public async Task Run([TimerTrigger("0 0 */24 * * *", RunOnStartup = true)] TimerInfo myTimer, ILogger log)
		{
			this._logger = log;
            _botClient.OnUpdate += OnUpdateReceived;
            _botClient.StartReceiving();

            await Task.CompletedTask;
        }
	}
}