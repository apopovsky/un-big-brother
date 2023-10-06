using Microsoft.Extensions.Logging;
using Telegram.Bot.Types;

namespace UnTaskAlert
{
    public interface ICommandProcessor
    {
        Task Process(Update update, ILogger log, CancellationToken cancellationToken);
    }
}
