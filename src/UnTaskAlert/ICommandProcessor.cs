using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Telegram.Bot.Types;

namespace UnTaskAlert
{
    public interface ICommandProcessor
    {
        Task Process(Update update, ILogger log);
    }
}
