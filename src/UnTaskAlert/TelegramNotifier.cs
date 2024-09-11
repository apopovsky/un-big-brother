using System.Text;
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
        const string alertMessage = "⚠️ *Alert\\!* ⚠️\n" +
                                    "No active tasks during working hours\\.\n" +
                                    "*You are working for free\\!* 😱";

        await _bot.SendTextMessageAsync(subscriber.TelegramId, alertMessage, parseMode: ParseMode.MarkdownV2);
    }

    public async Task ActiveTaskOutsideOfWorkingHours(Subscriber subscriber, ActiveTasksInfo activeTasksInfo)
    {
        // Formatear el mensaje de alerta con MarkdownV2 escapando caracteres especiales
        var text = "🕒 *Active task outside of working hours\\!* 🕒\n" + // Escapando el carácter '!'
                   "Doing some overtime, hah? 😉\n\n" +
                   "*Tasks:*\n";

        // Agregar las tareas en formato de lista
        text = activeTasksInfo.TasksInfo.Aggregate(text,
            (current, taskInfo) =>
                current +
                $"• {GetSingleTaskLink(taskInfo, ParseMode.MarkdownV2)} \\(Active: {taskInfo.ActiveTime:0.##} hs\\)\n");

        // Enviar el mensaje formateado con MarkdownV2
        await _bot.SendTextMessageAsync(subscriber.TelegramId, text, parseMode: ParseMode.MarkdownV2);
    }

    public async Task MoreThanSingleTaskIsActive(Subscriber subscriber, ActiveTasksInfo activeTasksInfo)
    {
        var message = "🚨 *More than one active task at the same time\\!* 🚨\n" + // Escapando el carácter '!'
                      "*Tasks:*\n";
        // Agregar las tareas en formato de lista
        message = activeTasksInfo.TasksInfo.Aggregate(message,
            (current, taskInfo) =>
                current +
                $"• {GetSingleTaskLink(taskInfo, ParseMode.MarkdownV2)}: {taskInfo.Title} \\(Active: {taskInfo.ActiveTime:0.##} hs\\)\n");
        await _bot.SendTextMessageAsync(subscriber.TelegramId, message, parseMode: ParseMode.MarkdownV2);
    }

    public async Task Ping(Subscriber subscriber)
    {
        await _bot.SendTextMessageAsync(subscriber.TelegramId, "I'm alive");
    }

    public async Task SendTimeReport(Subscriber subscriber, TimeReport timeReport)
    {
        // Generar el contenido del reporte y enviarlo como archivo adjunto
        var content = new StpdReportGenerator(_devOpsAddress).GenerateReport(timeReport);
        var byteArray = Encoding.UTF8.GetBytes(content);
        var contentStream = new MemoryStream(byteArray);
        var file = new InputFileStream(contentStream, "report.html");

        await _bot.SendDocumentAsync(subscriber.TelegramId, file, caption: "Your report.");
    
        // Crear el formato de la tabla del reporte
        var reportMessage = new StringBuilder();
        reportMessage.AppendLine($"📅 Your stats for period: {GetStatsPeriod(timeReport)}");
        reportMessage.AppendLine("```");
        reportMessage.AppendLine("----------------------------");
        reportMessage.AppendLine("| Metric          | Value  |");
        reportMessage.AppendLine("----------------------------");
        reportMessage.AppendLine($"| Estimated Hours | {timeReport.TotalEstimated,6:0.##} |");
        reportMessage.AppendLine($"| Completed Hours | {timeReport.TotalCompleted,6:0.##} |");
        reportMessage.AppendLine($"| Active Hours    | {timeReport.TotalActive,6:0.##} |");
        reportMessage.AppendLine($"| Expected Hours  | {(timeReport.Expected - timeReport.HoursOff),6:0.##} |");
        reportMessage.AppendLine($"| Hours off       | {timeReport.HoursOff,6:0.##} |");
        reportMessage.AppendLine("----------------------------");
        reportMessage.AppendLine("```");

        // Enviar el mensaje con MarkdownV2
        await _bot.SendTextMessageAsync(subscriber.TelegramId, reportMessage.ToString(), parseMode: ParseMode.MarkdownV2);
    }

    private static string GetStatsPeriod(TimeReport timeReport)
    {
        if (timeReport.StartDate == timeReport.EndDate)
        {
            return timeReport.StartDate.ToString("dd/MM/yyyy");
        }

        if (timeReport.StartDate.Day == 1 && timeReport.EndDate.Day == DateTime.DaysInMonth(timeReport.EndDate.Year, timeReport.EndDate.Month))
        {
            return timeReport.StartDate.ToString("MMMM yyyy");
        }

        return $"{timeReport.StartDate:dd/MM/yyyy} \\- {timeReport.EndDate:dd/MM/yyyy}";
    }


public async Task SendDetailedTimeReport(Subscriber subscriber, TimeReport timeReport, double offsetThreshold, bool includeSummary = true)
{
    const int maxTitleLength = 20; // Ajuste de la longitud máxima del título
    
    var baseUrl = new Url(_devOpsAddress).AppendPathSegment("/_workitems/edit/");
    var builder = new StringBuilder();
    var links = new HashSet<string>(); // Usar HashSet para almacenar enlaces únicos

    // Agregar encabezado de la tabla utilizando <pre> para el texto con formato fijo
    builder.AppendLine($"<b>Standup tasks {timeReport.StartDate.Date:dd/MM/yyyy}</b>{Environment.NewLine}");
    builder.AppendLine("<pre>");
    builder.AppendLine("Date  | ID     | Title                | C       ");
    builder.AppendLine("------|--------|----------------------|---------");

    foreach (var item in timeReport.WorkItemTimes.OrderBy(x => x.Date))
    {
        var title = item.Title;

        // Crear el enlace y almacenarlo en el HashSet
        var link = $"{baseUrl}{item.Id}";
        links.Add($"<a href=\"{link}\">Task {item.Id} - {title}</a>");

        // Dividir el título en líneas de no más de maxTitleLength caracteres
        var wrappedTitle = WrapText(title, maxTitleLength);
        var titleLines = wrappedTitle.Split([Environment.NewLine], StringSplitOptions.None);

        // Formatear la primera fila de la tabla con todas las columnas
        var message = $"{item.Date:dd/MM} | {item.Id,-6} | {titleLines[0],-maxTitleLength} | {item.Completed,7:F2}";
        builder.AppendLine(message);

        // Agregar líneas adicionales para el título, si las hay
        for (int i = 1; i < titleLines.Length; i++)
        {
            builder.AppendLine($"      |        | {titleLines[i].PadRight(maxTitleLength)} |         ");
        }
    }
    builder.AppendLine("------|--------|----------------------|---------");

    // Cerrar la etiqueta <pre>
    builder.AppendLine("</pre>");

    // Agregar los enlaces únicos al final del mensaje
    builder.AppendLine("<b>Links to tasks:</b>");
    foreach (var link in links)
    {
        builder.AppendLine(link);
    }

    // Enviar el mensaje completo
    if (builder.Length > 0)
    {
        await _bot.SendTextMessageAsync(subscriber.TelegramId, $"{builder}", parseMode: ParseMode.Html);
    }

    if (includeSummary)
    {
        // Incluir resumen de datos al final usando HTML
        await _bot.SendTextMessageAsync(subscriber.TelegramId,
            $"<b>Estimated Hours:</b> {timeReport.TotalEstimated:0.##}<br>" +
            $"<b>Completed Hours:</b> {timeReport.TotalCompleted:0.##}<br>" +
            $"<b>Active Hours:</b> {timeReport.TotalActive:0.##}<br>" +
            $"<b>Expected Hours:</b> {timeReport.Expected:0.##}",
            parseMode: ParseMode.Html);
    }
}

// Método para realizar el word wrap manual
private static string WrapText(string text, int maxLength)
{
    var lines = new List<string>();
    while (text.Length > maxLength)
    {
        // Cortar hasta el máximo de caracteres permitido
        var line = text.Substring(0, maxLength);
        lines.Add(line);

        // Remover la parte cortada y continuar
        text = text.Substring(maxLength);
    }

    // Agregar cualquier texto restante
    if (text.Length > 0)
    {
        lines.Add(text);
    }

    return string.Join(Environment.NewLine, lines);
}

    public async Task Progress(Subscriber subscriber)
    {
        await _bot.SendTextMessageAsync(subscriber.TelegramId, "Processing your request...");
    }

    public async Task ActiveTasks(Subscriber subscriber, ActiveTasksInfo activeTasksInfo)
    {
        var sb = new StringBuilder();
        sb.Append($"ℹ You have **{activeTasksInfo.ActiveTaskCount}** active task{(activeTasksInfo.ActiveTaskCount is > 1 or 0 ? "s" : string.Empty)}\\.{Environment.NewLine}");

        foreach (var taskInfo in activeTasksInfo.TasksInfo)
        {
            sb.AppendLine($@"• {GetSingleTaskLink(taskInfo, ParseMode.MarkdownV2)}: {taskInfo.Title} \(Active: {taskInfo.ActiveTime:0.##} hs\)");
        }

        await _bot.SendTextMessageAsync(subscriber.TelegramId, sb.ToString(), parseMode: ParseMode.MarkdownV2);
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
        var timeOff = subscriber.TimeOff?.OrderBy(off => off.Date).Select(i => $"{i.Date:dd/MM/yyyy} | {i.HoursOff,2} ").ToList();
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
        await _bot.SendTextMessageAsync(subscriber.TelegramId, text, parseMode: ParseMode.MarkdownV2);
    }

    private string GetSingleTaskLink(TaskInfo taskInfo, ParseMode format = ParseMode.Html)
    {
        var baseUrl = new Url(_devOpsAddress).AppendPathSegment("/_workitems/edit/");
        var taskUrl = baseUrl.AppendPathSegment(taskInfo.Id).ToString();

        return format switch
        {
            ParseMode.Markdown or ParseMode.MarkdownV2 => $"[{taskInfo.Id}]({taskUrl})", // Markdown format
            ParseMode.Html => $"<a href=\"{taskUrl}\">{taskInfo.Id}</a>", // HTML format
            _ => taskUrl, // Default: plain URL
        };
    }
}