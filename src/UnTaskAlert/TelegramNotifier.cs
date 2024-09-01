﻿using System.Text;
using Flurl;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using UnTaskAlert.Common;
using UnTaskAlert.Models;
using UnTaskAlert.Reports;
using Task = System.Threading.Tasks.Task;

namespace UnTaskAlert;

public interface ITelegramBotProvider
{
    ITelegramBotClient Client { get; }
}

public class TelegramBotProvider(ITelegramBotClient client) : ITelegramBotProvider
{
    public ITelegramBotClient Client { get; } = Arg.NotNull(client, nameof(client));
}

public class TelegramNotifier : INotifier
{
    private readonly ITelegramBotClient _bot;
    private readonly string _devOpsAddress;
    private const int MaxMessageLength = 4096;

    public static readonly string RequestEmailMessage =
        "I'm here to help you track your time. First, let me know your work email address.";

    public TelegramNotifier(IOptions<Config> options, ITelegramBotProvider botProvider)
    {
        Arg.NotNull(options, nameof(options));

        _devOpsAddress = options.Value.AzureDevOpsAddress;
        _bot = botProvider.Client;
    }

    public async Task Instruction(Subscriber subscriber)
    {
        var text = "I'm here to help you track your working time. " +
                   $"The following commands are available:{Environment.NewLine}" +
                   $"/standup - tasks of the previous work day{Environment.NewLine}" +
                   $"/active - show active tasks{Environment.NewLine}" +
                   $"/day - stats for today{Environment.NewLine}" +
                   $"/week - stats for the week{Environment.NewLine}" +
                   $"/month - stats for the month{Environment.NewLine}" +
                   $"/info - show account settings {Environment.NewLine}" +
                   $"/delete - delete account {Environment.NewLine}" +
                   $"/email - set email address {Environment.NewLine}" +
                   $"/healthcheck [threshold] - detailed report with a list of tasks where the difference between active and complete is bigger than a given threshold{Environment.NewLine}" +
                   "/help";

        await _bot.SendTextMessageAsync(subscriber.TelegramId, text, parseMode: ParseMode.Html);
    }

    public async Task NoActiveTasksDuringWorkingHours(Subscriber subscriber)
    {
        await _bot.SendTextMessageAsync(subscriber.TelegramId, "No active tasks during working hours. You are working for free.", parseMode: ParseMode.Html);
    }

    public async Task ActiveTaskOutsideOfWorkingHours(Subscriber subscriber, ActiveTasksInfo activeTasksInfo)
    {
        var text = $"Active task outside of working hours. Doing some overtime, hah?{Environment.NewLine}" +
                   $"Tasks: {Environment.NewLine}";

        foreach (var taskInfo in activeTasksInfo.TasksInfo)
        {
            text += $"-{GetSingleTaskLink(taskInfo)} (Active: {taskInfo.ActiveTime:0.##} hs){Environment.NewLine}";
        }

        await _bot.SendTextMessageAsync(subscriber.TelegramId, text, parseMode: ParseMode.Html);
    }

    public async Task MoreThanSingleTaskIsActive(Subscriber subscriber, ActiveTasksInfo tasksInfo)
    {
        var message = $"More than one active task at the same time!{Environment.NewLine}";
        message += $"Tasks: {Environment.NewLine}";
        tasksInfo.TasksInfo.ForEach(taskInfo =>
            message += $"-{GetSingleTaskLink(taskInfo)} (Active: {taskInfo.ActiveTime:0.##} hs){Environment.NewLine}");
        await _bot.SendTextMessageAsync(subscriber.TelegramId, message, parseMode: ParseMode.Html);
    }

    public async Task Ping(Subscriber subscriber)
    {
        await _bot.SendTextMessageAsync(subscriber.TelegramId, "I'm alive");
    }

    public async Task SendTimeReport(Subscriber subscriber, TimeReport timeReport)
    {
        var content = new StpdReportGenerator(_devOpsAddress).GenerateReport(timeReport);
        var byteArray = Encoding.UTF8.GetBytes(content);
        var contentStream = new MemoryStream(byteArray);
        var file = new InputFileStream(contentStream, "report.html");

        await _bot.SendDocumentAsync(subscriber.TelegramId, file, caption: "Your report.");
            
        await _bot.SendTextMessageAsync(subscriber.TelegramId,
            $"Your stats for {timeReport.StartDate.Date:yyyy-MM-dd}{timeReport.EndDate.Date:yyyy-MM-dd}{Environment.NewLine}{Environment.NewLine}" +
            $"Estimated Hours: {timeReport.TotalEstimated:0.##}{Environment.NewLine}" +
            $"Completed Hours: {timeReport.TotalCompleted:0.##}{Environment.NewLine}" +
            $"Active Hours: {timeReport.TotalActive:0.##}{Environment.NewLine}" +
            $"Expected Hours: {timeReport.Expected - timeReport.HoursOff:0.##}{Environment.NewLine}" +
            $"Hours off: {timeReport.HoursOff}");
    }

    public async Task SendDetailedTimeReport(Subscriber subscriber, TimeReport timeReport, double offsetThreshold, bool includeSummary = true)
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
                    $"{item.Date:dd-MM} <a href=\"{baseUrl + item.Id}\">{item.Id}</a> - {title} C:{item.Completed:F2} A:{item.Active:F2} E:{item.Estimated:F2} Off:{offset:P}";

                if (builder.Length + message.Length >= MaxMessageLength)
                {
                    await _bot.SendTextMessageAsync(subscriber.TelegramId, $"{builder}", parseMode: ParseMode.Html);
                    builder = new StringBuilder();
                }

                builder.AppendLine($"{message}");
            }
        }

        if (builder.Length > 0)
        {
            await _bot.SendTextMessageAsync(subscriber.TelegramId, $"{builder}", parseMode: ParseMode.Html);
        }

        if (includeSummary)
        {
            await _bot.SendTextMessageAsync(subscriber.TelegramId,
                $"Estimated Hours: {timeReport.TotalEstimated:0.##}{Environment.NewLine}" +
                $"Completed Hours: {timeReport.TotalCompleted:0.##}{Environment.NewLine}" +
                $"Active Hours: {timeReport.TotalActive:0.##}{Environment.NewLine}" +
                $"Expected Hours: {timeReport.Expected:0.##}", parseMode: ParseMode.Markdown);
        }
    }

    public async Task Progress(Subscriber subscriber)
    {
        await _bot.SendTextMessageAsync(subscriber.TelegramId, "Processing your request...");
    }

    public async Task ActiveTasks(Subscriber subscriber, ActiveTasksInfo activeTasksInfo)
    {
        var sb = new StringBuilder();
        sb.AppendFormat("{0} has {1} active task{2}.{3}", subscriber.Email, activeTasksInfo.ActiveTaskCount, activeTasksInfo.ActiveTaskCount > 1 || activeTasksInfo.ActiveTaskCount == 0 ? "s" : string.Empty, Environment.NewLine);
        if (activeTasksInfo.ActiveTaskCount != 0)
        {
            var nextLine = false;
            foreach (var taskInfo in activeTasksInfo.TasksInfo)
            {
                if (nextLine)
                {
                    sb.Append(Environment.NewLine);
                }
                else
                {
                    nextLine = true;
                }
                sb.AppendFormat("-{0} (Active: {1:0.##} hs)", GetSingleTaskLink(taskInfo), taskInfo.ActiveTime);
            }
        }
        await _bot.SendTextMessageAsync(subscriber.TelegramId, sb.ToString(), parseMode: ParseMode.Html);
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

    public async Task Typing(string chatId, CancellationToken cancellationToken)
    {
        await _bot.SendChatActionAsync(chatId, ChatAction.Typing, cancellationToken: cancellationToken);
    }

    public async Task RequestEmail(string chatId)
    {
        await _bot.SendTextMessageAsync(chatId, RequestEmailMessage);
    }

    public async Task Respond(long chatId, string message)
    {
        await _bot.SendTextMessageAsync(chatId, message);
    }

    public async Task AccountInfo(Subscriber subscriber)
    {
        // Crear encabezado de tabla para la sección "Time off" usando mono-espaciado
        const string timeOffHeader = "Fecha......|.Horas";

        // Formatear cada entrada de "Time off" como una fila de la tabla con espacios para alineación
        var timeOff = subscriber?.TimeOff?.OrderBy(off => off.Date).Select(i => $"{i.Date:dd/MM/yyyy} | {i.HoursOff,2} ").ToList();
        var timeOffInfo = timeOff == null ? "n/a" : string.Join(Environment.NewLine, timeOff);

        // Concatenar el encabezado de la tabla con el contenido
        var timeOffTable = $"{timeOffHeader}\n{timeOffInfo}";

        // Utilizar texto pre-formateado para enviar la tabla con un formato más legible
        var text = $"TelegramId: {subscriber.TelegramId}{Environment.NewLine}" +
                   $"Email: {subscriber.Email}{Environment.NewLine}" +
                   $"Working hours (UTC): {subscriber.StartWorkingHoursUtc}-{subscriber.EndWorkingHoursUtc}{Environment.NewLine}" +
                   $"Is account verified: {subscriber.IsVerified}{Environment.NewLine}" +
                   $"Hours per day: {subscriber.HoursPerDay}{Environment.NewLine}" +
                   $"LastNoActiveTasksAlert: {subscriber.LastNoActiveTasksAlert}{Environment.NewLine}" +
                   $"LastMoreThanSingleTaskIsActiveAlert: {subscriber.LastMoreThanSingleTaskIsActiveAlert}{Environment.NewLine}" +
                   $"LastActiveTaskOutsideOfWorkingHoursAlert: {subscriber.LastActiveTaskOutsideOfWorkingHoursAlert}{Environment.NewLine}" +
                   $"*Time off:*\n```{timeOffTable}```"; // Usar texto pre-formateado con backticks

        // Enviar el mensaje con parse_mode Markdown para mantener el formato
        await _bot.SendTextMessageAsync(subscriber.TelegramId, text, parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown);
    }

    private List<string> GetTasksLinks(ActiveTasksInfo activeTasksInfo)
    {
        return activeTasksInfo.TasksInfo.Select(taskInfo => $"{GetSingleTaskLink(taskInfo)}").ToList();
    }

    private string GetSingleTaskLink(TaskInfo taskInfo)
    {
        var baseUrl = new Url(_devOpsAddress).AppendPathSegment("/_workitems/edit/");
        return $"<a href=\"{baseUrl.AppendPathSegment(taskInfo.Id)}\">{taskInfo.Id}</a>";
    }
}