using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Telegram.Bot;
using Telegram.Bot.Args;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot.Types.Enums;
using UnTaskAlert.Common;

namespace UnTaskAlert.MyNamespace
{
	public class TelegramBotListener : ITelegramBotListener
	{
		private readonly ITelegramBotClient _botClient;
		private readonly ICommandProcessor _commandProcessor;
		private ILogger _logger;
		private readonly Config _config;

		public TelegramBotListener(ICommandProcessor service, IOptions<Config> options, IDbAccessor dbAccessor, ITelegramBotClient botClient)
		{
			_botClient = botClient;
			_commandProcessor = Arg.NotNull(service, nameof(service));
			_config = Arg.NotNull(options.Value, nameof(options));
		}

		public void OnUpdateReceived(object sender, UpdateEventArgs updateEventArgs)
		{
			try
			{
				if (updateEventArgs.Update.Type == UpdateType.Message)
				{
					_logger.LogInformation($"Message received from: {updateEventArgs.Update.Message.From}. Message: {updateEventArgs.Update.Message.Text}");
					Task.Run(() => _commandProcessor.Process(updateEventArgs.Update, _logger).GetAwaiter().GetResult())
                        .ContinueWith((task) => {
                            var exception = task.Exception;
                            _logger.LogError(new EventId(), exception, exception.Message);
                            _botClient.SendTextMessageAsync(updateEventArgs.Update.Message.Chat.Id, "Could not process your request");

                        }, TaskContinuationOptions.OnlyOnFaulted);
				}
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
			if (_config.DebugLocal)
			{
				_botClient.OnUpdate += OnUpdateReceived;
				_botClient.StartReceiving();
			}
		}

		
}
}