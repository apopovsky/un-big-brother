using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using UnTaskAlert.Common;
using UnTaskAlert.Models;

namespace UnTaskAlert
{
    public class TelegramNotifier : INotifier
    {
        private readonly TelegramBotClient _bot;

        public TelegramNotifier(IOptions<Config> options)
        {
            Arg.NotNull(options, nameof(options));

            _bot = new TelegramBotClient(options.Value.TelegramBotKey);
        }

        public async Task Instruction(Subscriber subscriber)
        {
            var text = $"The first thing you need to do is to set your email address:{Environment.NewLine}" +
                       $"/email <email>{Environment.NewLine}" +
                       $"{Environment.NewLine}" +
                       $"Then the following commands can be used:{Environment.NewLine}" +
                       $"/day{Environment.NewLine}" +
                       $"/week{Environment.NewLine}" +
                       $"/month{Environment.NewLine}" +
                       $"/active{Environment.NewLine}" +
                       $"/help{Environment.NewLine}" +
                       $"Only @un.org emails are supported";
            await _bot.SendTextMessageAsync(subscriber.TelegramId, text);
        }

        public async Task NoActiveTasksDuringWorkingHours(Subscriber subscriber)
        {
            await _bot.SendTextMessageAsync(subscriber.TelegramId, "No active tasks during working hours. You are working for free.");
        }

        public async Task ActiveTaskOutsideOfWorkingHours(Subscriber subscriber, ActiveTaskInfo activeTaskInfo)
        {
            var text = $"Active task outside of working hours. Doing some overtime, hah?{Environment.NewLine}" +
                       $"Tasks: {string.Join(", ", activeTaskInfo.WorkItemsIds.Select(i => i.ToString()))}";
            await _bot.SendTextMessageAsync(subscriber.TelegramId, text);
        }

        public async Task MoreThanSingleTaskIsActive(Subscriber subscriber)
        {
            await _bot.SendTextMessageAsync(subscriber.TelegramId, "More than one active task at the same time. This is wrong, do something.");
        }

        public async Task Ping(Subscriber subscriber)
        {
            await _bot.SendTextMessageAsync(subscriber.TelegramId, "I'm alive");
        }

        public async Task SendTimeReport(Subscriber subscriber, TimeReport timeReport)
        {
            await _bot.SendTextMessageAsync(subscriber.TelegramId,
                $"Your stats since {timeReport.StartDate.Date:yyyy-MM-dd}{Environment.NewLine}{Environment.NewLine}" +
                $"Estimated Hours: {timeReport.TotalEstimated:0.##}{Environment.NewLine}" +
                $"Completed Hours: {timeReport.TotalCompleted:0.##}{Environment.NewLine}" +
                $"Active Hours: {timeReport.TotalActive:0.##}{Environment.NewLine}" +
                $"Expected Hours: {timeReport.Expected:0.##}");
        }

        public async Task Progress(Subscriber subscriber)
        {
            await _bot.SendTextMessageAsync(subscriber.TelegramId, "Processing your request...");
        }

        public async Task ActiveTasks(Subscriber subscriber, ActiveTaskInfo activeTaskInfo)
        {
            var text = $"{subscriber.Email} has {activeTaskInfo.ActiveTaskCount} active tasks{Environment.NewLine}";
            if (activeTaskInfo.ActiveTaskCount != 0)
            {
                text +=
                    $"Tasks: {string.Join(", ", activeTaskInfo.WorkItemsIds.Select(i => i.ToString()))}";
            }

            await _bot.SendTextMessageAsync(subscriber.TelegramId, text);
        }

        public async Task IncorrectEmail(string chatId)
        {
            await _bot.SendTextMessageAsync(chatId, "Incorrect email address");
        }

        public async Task EmailUpdated(Subscriber subscriber)
        {
            var text = $"Email address is set to {subscriber.Email}, but is not yet confirmed.{Environment.NewLine}" +
                       $"Check you mailbox and verify the pin number by sending it to the bot.{Environment.NewLine}{Environment.NewLine}" +
                       $"Your working hours (UTC) are set to: {subscriber.StartWorkingHoursUtc} - {subscriber.EndWorkingHoursUtc}{Environment.NewLine}" +
                       $"Hours per day is {subscriber.HoursPerDay}{Environment.NewLine}" +
                       $"Contact vadim.grayfer@un.org to update your working hours";
            await _bot.SendTextMessageAsync(subscriber.TelegramId, text);
        }

        public async Task NoEmail(string chatId)
        {
            await _bot.SendTextMessageAsync(chatId, "Your email is not set. Use /help command to fix it.");
        }

        public async Task AccountVerified(Subscriber subscriber)
        {
            await _bot.SendTextMessageAsync(subscriber.TelegramId, "Your account is verified. Now you are able to request reports.");
        }

        public async Task CouldNotVerifyAccount(Subscriber subscriber)
        {
            await _bot.SendTextMessageAsync(subscriber.TelegramId, "Your account could not be verified.");
        }
    }
}
