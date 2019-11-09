using Telegram.Bot.Args;

namespace UnTaskAlert.MyNamespace
{
	public interface ITelegramBotListener
	{
		void OnUpdateReceived(object sender, UpdateEventArgs updateEventArgs);
	}
}