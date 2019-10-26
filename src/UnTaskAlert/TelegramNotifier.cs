﻿using System.Threading.Tasks;
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

        public async Task NoActiveTasksDuringWorkingHours(Subscriber subscriber)
        {
            await _bot.SendTextMessageAsync(subscriber.TelegramId, "No active tasks during working hours. You are working for free.");
        }

        public async Task ActiveTaskOutsideOfWorkingHours(Subscriber subscriber)
        {
            await _bot.SendTextMessageAsync(subscriber.TelegramId, "Active task outside of working hours. Doing some overtime, hah?");
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
                await _bot.SendTextMessageAsync(subscriber.TelegramId, "Your stats for this month:");
                await _bot.SendTextMessageAsync(subscriber.TelegramId, $"Estimated Time:{timeReport.TotalEstimated}");
                await _bot.SendTextMessageAsync(subscriber.TelegramId, $"Completed Time:{timeReport.TotalCompleted}");
                await _bot.SendTextMessageAsync(subscriber.TelegramId, $"Active Time:{timeReport.TotalActive}");
        }
    }
}
