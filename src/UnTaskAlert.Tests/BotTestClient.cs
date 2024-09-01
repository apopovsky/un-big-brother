using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Newtonsoft.Json;
using NUnit.Framework;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using UnTaskAlert.Functions;

namespace UnTaskAlert.Tests
{
    public class BotTestClient
    {
        private readonly TelegramBotFunction _target;
        private readonly IMailSender _mailSender;
        private readonly ILogger _logger;
        private readonly Stack<string> _messages = new Stack<string>();
        private readonly string _chatId;

        public BotTestClient(string chatId, TelegramBotFunction target)
        {
            _chatId = chatId;
            _target = target;

            var mailSenderMock = new Mock<IMailSender>();
            _mailSender = mailSenderMock.Object;

            var serviceProvider = new ServiceCollection()
                .AddLogging(cfg =>
                {
                    cfg.AddConsole();
                })
                .Configure<LoggerFilterOptions>(cfg => cfg.MinLevel = LogLevel.Debug).BuildServiceProvider();
            _logger = serviceProvider.GetService<ILogger<TelegramBotFunctionTests>>();

            var testConfig = new Config
            {
                AzureDevOpsAccessToken = "", // todo: move to config
                AzureDevOpsAddress = "", // todo: move to config
                // todo: move to config
                CosmosDbConnectionString = "",
                EmailDomain = "@fake.fake",
                EMailPassword = "",
                FromEmailAddress = "bot@bot.com",
                Smtp = "smtp.local",
                TelegramBotKey = "fake_key"
            };
            var optionsMock = new Mock<IOptions<Config>>();
            optionsMock.Setup(i => i.Value).Returns(testConfig);

            var backlogAccessor = new BacklogAccessor(new QueryBuilder());

            var botMock = new Mock<ITelegramBotClient>();
            botMock.Setup(i => i.SendTextMessageAsync(
                    It.IsAny<ChatId>(),
                    It.IsAny<string>(),
                    It.IsAny<int?>(),
                    It.IsAny<ParseMode>(),
                    It.IsAny<IEnumerable<MessageEntity>>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<int>(),
                    It.IsAny<bool>(),
                    It.IsAny<IReplyMarkup>(),
                    It.IsAny<CancellationToken>()))
                .Callback(new InvocationAction(action => _messages.Push((string)action.Arguments[1])));

            var botProvider = new Mock<ITelegramBotProvider>();
            botProvider.SetupGet(i => i.Client).Returns(botMock.Object);
            var notifier = new TelegramNotifier(optionsMock.Object, botProvider.Object);

            var reportingService = new ReportingService(notifier, backlogAccessor);

            throw new NotImplementedException();
            //var dbAccessor = new DbAccessor(optionsMock.Object);

            //var commandProcessor = new CommandProcessor(
            //    notifier,
            //    reportingService,
            //    dbAccessor,
            //    _mailSender,
            //    new FakePingGenerator(),
            //    optionsMock.Object);

            //_target = new TelegramBotFunction(commandProcessor);
        }

        public BotTestClient Send(string message)
        {
            var request = GetRequest(_chatId, message);
            //_target.Run(request, _logger).Wait();

            return this;
        }

        public BotTestClient CheckResponse(Action<string> action)
        {
            var reply = _messages.Pop();
            action(reply);

            return this;
        }

        public BotTestClient CheckResponse(string expected)
        {
            var reply = _messages.Pop();
            Assert.Equals(reply, TelegramNotifier.RequestEmailMessage);

            return this;
        }

        public BotTestClient DeleteAccount() => Send("/delete").CheckResponse("Account is deleted");

        private HttpRequest GetRequest(string chatId, string text)
        {
            var body = new Update
            {
                Message = new Message
                {
                    Chat = new Chat
                    {
                        Id = int.Parse(chatId)
                    },
                    Text = text,
                    Date = DateTime.Now
                }
            };
            var serializedBody = JsonConvert.SerializeObject(body, Formatting.None);

            var result = new Mock<HttpRequest>();
            result.SetupGet(i => i.Body).Returns(GenerateStreamFromString(serializedBody));

            return result.Object;
        }

        private static Stream GenerateStreamFromString(string s)
        {
            var stream = new MemoryStream();
            var writer = new StreamWriter(stream);
            writer.Write(s);
            writer.Flush();
            stream.Position = 0;
            return stream;
        }
    }
}
