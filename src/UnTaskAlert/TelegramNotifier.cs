﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Flurl;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.Services.Common.Internal;
using Newtonsoft.Json;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InputFiles;
using UnTaskAlert.Common;
using UnTaskAlert.Models;
using JsonSerializerSettings = Newtonsoft.Json.JsonSerializerSettings;
using NullValueHandling = Newtonsoft.Json.NullValueHandling;
using Task = System.Threading.Tasks.Task;

namespace UnTaskAlert
{
    public interface ITelegramBotProvider
    {
        ITelegramBotClient Client { get; }
    }

    public class TelegramBotProvider : ITelegramBotProvider
    {
        public TelegramBotProvider(ITelegramBotClient client)
        {
            this.Client = client;
        }

        public ITelegramBotClient Client { get; }
    }

    public class TelegramNotifier : INotifier
    {
        private readonly ITelegramBotClient _bot;

        private static readonly int maxMessageLength = 4096;

        public TelegramNotifier(IOptions<Config> options, ITelegramBotProvider botProvider)
        {
            Arg.NotNull(options, nameof(options));

            _devOpsAddress = options.Value.AzureDevOpsAddress;
            _bot = botProvider.Client;
        }

        public async Task Instruction(Subscriber subscriber)
        {
            var text = $"I'm here to help you track your working time. " +
                       $"The following commands are available:{Environment.NewLine}" +
                       $"/info - show account settings {Environment.NewLine}" +
                       $"/delete - delete account {Environment.NewLine}" +
                       $"/email - set email address {Environment.NewLine}" +
                       $"/day - stats for today{Environment.NewLine}" +
                       $"/week - stats for the week{Environment.NewLine}" +
                       $"/month - stats for the month{Environment.NewLine}" +
                       $"/active - show active tasks{Environment.NewLine}" +
                       $"/healthcheck [threshold] - detailed report with a list of tasks where the difference between active and complete is bigger than a given threshold{Environment.NewLine}" +
                       $"/standup - tasks of the previous work day{Environment.NewLine}" +
                       "/help";

            await _bot.SendTextMessageAsync(subscriber.TelegramId, text);
        }

        public async Task NoActiveTasksDuringWorkingHours(Subscriber subscriber)
        {
            await _bot.SendTextMessageAsync(subscriber.TelegramId, "No active tasks during working hours. You are working for free.");
        }

        public async Task ActiveTaskOutsideOfWorkingHours(Subscriber subscriber, ActiveTaskInfo activeTaskInfo)
        {
            var baseUrl = new Url(_devOpsAddress).AppendPathSegment("/_workitems/edit/");
            var text = $"Active task outside of working hours. Doing some overtime, hah?{Environment.NewLine}" +
                       $"Tasks: {activeTaskInfo.WorkItemsIds.Select(id => $"<a href=\"{baseUrl.AppendPathSegment(id)}\">{id}</a>{Environment.NewLine}")}";
            
			await _bot.SendTextMessageAsync(subscriber.TelegramId, text, ParseMode.Html);
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
			var reportData = JsonConvert.SerializeObject(timeReport, Formatting.Indented);
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = "UnTaskAlert.Reports.DetailReport.html";

            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            using (StreamReader reader = new StreamReader(stream))
            {
                string content = reader.ReadToEnd();
                content=content.Replace("'@@DATA@@'", reportData);
				byte[] byteArray = Encoding.UTF8.GetBytes(content);
                MemoryStream contentStream = new MemoryStream(byteArray);

                var file = new InputOnlineFile(contentStream) {FileName = "report.html"};
                await _bot.SendDocumentAsync(subscriber.TelegramId, file, caption: "Your report.");
            }

			await _bot.SendTextMessageAsync(subscriber.TelegramId,
                $"Your stats since {timeReport.StartDate.Date:yyyy-MM-dd}{Environment.NewLine}{Environment.NewLine}" +
                $"Estimated Hours: {timeReport.TotalEstimated:0.##}{Environment.NewLine}" +
                $"Completed Hours: {timeReport.TotalCompleted:0.##}{Environment.NewLine}" +
                $"Active Hours: {timeReport.TotalActive:0.##}{Environment.NewLine}" +
                $"Expected Hours: {timeReport.Expected:0.##}");
        }

        public async Task SendDetailedTimeReport(Subscriber subscriber, TimeReport timeReport, double offsetThreshold)
        {
            await _bot.SendTextMessageAsync(subscriber.TelegramId,
                $"Your stats since {timeReport.StartDate.Date:yyyy-MM-dd}");

            const int maxTitleLength = 50;
			//Support threshold values from decimal or percentage
            if (offsetThreshold > 1)
            {
                offsetThreshold /= 100;
            }

            var baseUrl = new Url(_devOpsAddress).AppendPathSegment("/_workitems/edit/");
            var builder = new StringBuilder();
            foreach (var item in timeReport.WorkItemTimes.OrderBy(x => x.Date))
            {
                var title = item.Title;
                if (title.Length > maxTitleLength) title = title.Substring(0, maxTitleLength);
                title = title.PadRight(maxTitleLength);

                var offset = Math.Abs(item.Active - item.Completed) / item.Active;
                
                if (offset > offsetThreshold)
                {
                    var message =
                        $"{item.Date:dd-MM} <a href=\"{baseUrl.AppendPathSegment(item.Id)}\">{item.Id}</a> - {title} C:{item.Completed:F2} A:{item.Active:F2} E:{item.Estimated:F2} Off:{offset:P}";

                    if (builder.Length + message.Length >= maxMessageLength)
                    {
                        await _bot.SendTextMessageAsync(subscriber.TelegramId, $"{builder}", ParseMode.Html);
                        builder = new StringBuilder();
                    }

                    builder.AppendLine($"{message}");
                }
            }

            if (builder.Length > 0)
            {
                await _bot.SendTextMessageAsync(subscriber.TelegramId, $"{builder}", ParseMode.Html);
            }

            await _bot.SendTextMessageAsync(subscriber.TelegramId,
                $"Estimated Hours: {timeReport.TotalEstimated:0.##}{Environment.NewLine}" +
                $"Completed Hours: {timeReport.TotalCompleted:0.##}{Environment.NewLine}" +
                $"Active Hours: {timeReport.TotalActive:0.##}{Environment.NewLine}" +
                $"Expected Hours: {timeReport.Expected:0.##}", ParseMode.Markdown);
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
            var text = $"Email address is set to {subscriber.Email}, but is not yet verified.{Environment.NewLine}" +
                       "Please check you mailbox and send PIN to this chat.";
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

        public async Task Typing(string chatId)
        {
            await _bot.SendChatActionAsync(chatId, ChatAction.Typing);
        }

        public async Task RequestEmail(string chatId)
        {
            await _bot.SendTextMessageAsync(chatId, RequestEmailMessage);
        }

        public async Task AccountInfo(Subscriber subscriber)
        {
            var text = $"TelegramId: {subscriber.TelegramId}{Environment.NewLine}" +
                       $"Email: {subscriber.Email}{Environment.NewLine}" +
                       $"Working hours (UTC): {subscriber.StartWorkingHoursUtc}-{subscriber.EndWorkingHoursUtc}{Environment.NewLine}" +
                       $"Is account verified: {subscriber.IsVerified}{Environment.NewLine}" +
                       $"Hours per day: {subscriber.HoursPerDay}{Environment.NewLine}" +
                       $"LastNoActiveTasksAlert: {subscriber.LastNoActiveTasksAlert}{Environment.NewLine}" +
                       $"LastMoreThanSingleTaskIsActiveAlert: {subscriber.LastMoreThanSingleTaskIsActiveAlert}{Environment.NewLine}" +
                       $"LastActiveTaskOutsideOfWorkingHoursAlert: {subscriber.LastActiveTaskOutsideOfWorkingHoursAlert}{Environment.NewLine}";
            await _bot.SendTextMessageAsync(subscriber.TelegramId, text);
        }

        public static string RequestEmailMessage =
            "I'm here to help you track your time. First, let me know your work email address.";

        private readonly string _devOpsAddress;
    }
}
