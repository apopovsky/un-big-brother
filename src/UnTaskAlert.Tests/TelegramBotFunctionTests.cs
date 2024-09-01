using Moq;
using NUnit.Framework;
using System;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using System.IO;
using System.Threading.Tasks;
using Telegram.Bot.Types;

namespace UnTaskAlert.Tests
{
    //[TestFixture]
    public class TelegramBotFunctionTests
    {
        

        // todo: use stubs for sending email and telegram messages. what about cosmosdb?
        [SetUp]
        public void Setup()
        {
            
        }

        [Test]
        public void Test1()
        {
            Assert.Pass();
        }

        // 2. new user sets email
        // 3. new user confirms email
        //[Test]
        public void NewUser_RegistersAndVerifies()
        {
            // todo: clean db from this user
            // todo: fix fluent api to async pattern
            // todo: call DeleteAccount() in case of an error

            var chatId = "666";
            var client = new BotTestClient(chatId, null);

            client
                .Send("hey")
                .CheckResponse(TelegramNotifier.RequestEmailMessage)
                .Send("fake@fake.fake")
                .CheckResponse(s => Assert.That(s.StartsWith("Email address is set to fake@fake.fake")))
                .Send("4567")
                .CheckResponse(s => Assert.Equals(s, "Your account could not be verified."))
                .Send(FakePingGenerator.Pin.ToString())
                .CheckResponse(s => Assert.Equals(s, "Your account is verified. Now you are able to request reports."))
                .Send("/delete")
                .CheckResponse("Account is deleted");

            //// request report /day
            //request = GetRequest(chatId, "/day");
            //await _target.Run(request, _logger);
            //reply = _messages.Pop();
            //Assert.IsTrue(reply.StartsWith("Your stats since"));

            //// request report /week
            //request = GetRequest(chatId, "/week");
            //await _target.Run(request, _logger);
            //reply = _messages.Pop();
            //Assert.IsTrue(reply.StartsWith("Your stats since"));

            //// request report /month
            //request = GetRequest(chatId, "/month");
            //await _target.Run(request, _logger);
            //reply = _messages.Pop();
            //Assert.IsTrue(reply.StartsWith("Your stats since"));

            //request = GetRequest(chatId, "/delete");
            //await _target.Run(request, _logger);
        }

        //[Test]
        public void NewUserEntersWrongPinAndIsBlocked()
        {
            var chatId = "667";
            var client = new BotTestClient(chatId, null);

            client
                .Send("hey")
                .CheckResponse(TelegramNotifier.RequestEmailMessage)
                .Send("fake@fake.fake")
                .CheckResponse(s => Assert.That(s.StartsWith("Email address is set to fake@fake.fake")))
                // 1st attempt
                .Send("1111")
                .CheckResponse(s => Assert.Equals(s, "Your account could not be verified."))
                // 2nd attempt
                .Send("2222")
                .CheckResponse(s => Assert.Equals(s, "Your account could not be verified."))
                // 3rd attempt
                .Send("3333")
                .CheckResponse(s => Assert.Equals(s, "Your account could not be verified."))
                // User is locked and cannot use the correct pin
                .Send(FakePingGenerator.Pin.ToString())
                .CheckResponse(
                    s => Assert.Equals(s, "Your account could not be verified."))
                // User is not verified and cannot request reports
                .Send("/day")
                .CheckResponse(s => Assert.Equals(s, "Your account could not be verified."));
            //.DeleteAccount();
        }

        //[Test]
        public void NewUserCanVerifyAccountOnLastAttempt()
        {
            var chatId = "668";
            var client = new BotTestClient(chatId, null);

            client
                .Send("hey")
                .CheckResponse(TelegramNotifier.RequestEmailMessage)
                .Send("fake@fake.fake")
                .CheckResponse(s => Assert.That(s.StartsWith("Email address is set to fake@fake.fake")))
                // 1st attempt
                .Send("1111")
                .CheckResponse(s => Assert.Equals(s, "Your account could not be verified."))
                // 2nd attempt
                .Send("2222")
                .CheckResponse(s => Assert.Equals(s, "Your account could not be verified."))
                // 3rd attempt
                .Send(FakePingGenerator.Pin.ToString())
                .CheckResponse(s => Assert.Equals(s, "Your account is verified.Now you are able to request reports."));
            //.DeleteAccount();
        }

        // 1. new user sends message
        // 2. new user sets email
        // 3. new user resets email
        // 4. new user confirms email
        // 5. new user requests reports
        //[Test]
        public Task NewUser1() => throw new NotImplementedException();

        // 1. new user sends message
        // 2. new user sets email
        // 3. cannot verify email and is blocked
        //[Test]
        public Task NewUser2() => throw new NotImplementedException();

        // 1. new user sends message
        // 2. new user incorrect email
        //[Test]
        public Task NewUser3() => throw new NotImplementedException();

        // todo: monitoring tests

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