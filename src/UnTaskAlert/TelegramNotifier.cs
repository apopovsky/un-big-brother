using System.Text;
using Flurl;
using Microsoft.Extensions.Logging;
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
    private readonly ILogger<TelegramNotifier> _logger;
    private readonly ITelegramBotClient _bot;
    private readonly string _devOpsAddress;

    private const string RequestEmailMessage = "I'm here to help you track your time. First, let me know your work email address.";

    public TelegramNotifier(IOptions<Config> options, ITelegramBotProvider botProvider, ILogger<TelegramNotifier> logger)
    {
        _logger = logger;
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
                   $"/pr - show active pull requests{Environment.NewLine}" +
                   $"/day - stats for today{Environment.NewLine}" +
                   $"/week - stats for the week{Environment.NewLine}" +
                   $"/month - stats for the month{Environment.NewLine}" +
                   $"/info - show account settings {Environment.NewLine}" +
                   $"/delete - delete account {Environment.NewLine}" +
                   $"/email - set email address {Environment.NewLine}" +
                   $"/healthcheck [threshold] - detailed report with a list of tasks where the difference between active and complete is bigger than a given threshold{Environment.NewLine}" +
                   "/help";

        await _bot.SendMessage(subscriber.TelegramId, text, parseMode: ParseMode.Html);
    }

    public async Task NoActiveTasksDuringWorkingHours(Subscriber subscriber)
    {
        const string alertMessage = "⚠️ *Alert\\!* ⚠️\n" +
                                    "No active tasks during working hours\\.\n" +
                                    "*You are working for free\\!* 😱";

        await _bot.SendMessage(subscriber.TelegramId, alertMessage, parseMode: ParseMode.MarkdownV2);
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
                $"{current}{GetSingleTaskFormatted(taskInfo)}\n");

        // Enviar el mensaje formateado con MarkdownV2
        await _bot.SendMessage(subscriber.TelegramId, text, parseMode: ParseMode.MarkdownV2);
    }

    public async Task MoreThanSingleTaskIsActive(Subscriber subscriber, ActiveTasksInfo activeTasksInfo)
    {
        var message = "🚨 *More than one active task at the same time\\!* 🚨\n" + // Escapando el carácter '!'
                      "*Tasks:*\n";
        // Agregar las tareas en formato de lista
        message = activeTasksInfo.TasksInfo.Aggregate(message,
            (current, taskInfo) => $"{current}{GetSingleTaskFormatted(taskInfo)}\n");
        await _bot.SendMessage(subscriber.TelegramId, message, parseMode: ParseMode.MarkdownV2);
    }

    public async Task Ping(Subscriber subscriber)
    {
        await _bot.SendMessage(subscriber.TelegramId, "I'm alive");
    }

    public async Task SendTimeReport(Subscriber subscriber, TimeReport timeReport)
    {
        var content = new StpdReportGenerator(_devOpsAddress).GenerateReport(timeReport);
        var byteArray = Encoding.UTF8.GetBytes(content);
        var contentStream = new MemoryStream(byteArray);
        var file = new InputFileStream(contentStream, "report.html");

        await _bot.SendDocument(subscriber.TelegramId, file, caption: "Your report.");

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

        await _bot.SendMessage(subscriber.TelegramId, reportMessage.ToString(), parseMode: ParseMode.MarkdownV2);

        // Add anomalous days section if report spans multiple days
        if (timeReport.StartDate.Date != timeReport.EndDate.Date)
        {
            var anomalousDays = GetAnomalousDays(timeReport, subscriber.HoursPerDay == 0 ? 8 : subscriber.HoursPerDay);
            if (anomalousDays.Any())
            {
                var anomalousMessage = new StringBuilder();
                anomalousMessage.AppendLine("```");
                anomalousMessage.AppendLine("⚠️ Anomalous Days (≠8h):");
                anomalousMessage.AppendLine("----------------------------");
                foreach (var day in anomalousDays)
                {
                    anomalousMessage.AppendLine($"\\- {day.Date:ddd dd/MM}: {day.Hours:0.##}h");
                }
                anomalousMessage.AppendLine("```");

                await _bot.SendMessage(subscriber.TelegramId, anomalousMessage.ToString(), parseMode: ParseMode.MarkdownV2);
            }
        }
    }

    private static List<(DateTime Date, double Hours)> GetAnomalousDays(TimeReport timeReport, int expectedHoursPerDay)
    {
        return timeReport.WorkItemTimes
            .GroupBy(w => w.Date.Date)
            .Select(g => new { Date = g.Key, Hours = g.Sum(w => w.Completed) })
            .Where(d => Math.Abs(d.Hours - expectedHoursPerDay) > 0.01)
            .OrderBy(d => d.Date)
            .Select(d => (d.Date, d.Hours))
            .ToList();
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
    builder.AppendLine($"<b>Tasks for period {GetStatsPeriod(timeReport)}</b>{Environment.NewLine}");
    builder.AppendLine("<pre>");
    builder.AppendLine("Date  | ID     | Title                | A    | C    ");
    builder.AppendLine("------|--------|----------------------|------|------");

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
        var message = $"{item.Date:dd/MM} | {item.Id,-6} | {titleLines[0],-maxTitleLength} | {item.Active,4:F2} | {item.Completed,4:F2}";
        builder.AppendLine(message);

        // Agregar líneas adicionales para el título, si las hay
        for (int i = 1; i < titleLines.Length; i++)
        {
            builder.AppendLine($"      |        | {titleLines[i].PadRight(maxTitleLength)} |      |      ");
        }
    }
    builder.AppendLine("------|--------|----------------------|------|-----");
    builder.AppendLine($"      |        |{"", -22}| {timeReport.TotalActive,4:F2} | {timeReport.TotalCompleted,4:F2} ");

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
        await _bot.SendMessage(subscriber.TelegramId, $"{builder}", parseMode: ParseMode.Html);
    }

    if (includeSummary)
    {
        // Incluir resumen de datos al final usando HTML
        await _bot.SendMessage(subscriber.TelegramId,
            $"<b>Estimated Hours:</b> {timeReport.TotalEstimated:0.##}\n" +
            $"<b>Completed Hours:</b> {timeReport.TotalCompleted:0.##}\n" +
            $"<b>Active Hours:</b> {timeReport.TotalActive:0.##}\n" +
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
        var line = text[..maxLength];
        lines.Add(line);

        // Remover la parte cortada y continuar
        text = text[maxLength..];
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
        await _bot.SendMessage(subscriber.TelegramId, "Processing your request...");
    }

    public async Task ActiveTasks(Subscriber subscriber, ActiveTasksInfo activeTasksInfo)
    {
        var sb = new StringBuilder();
        sb.Append($"ℹ You have **{activeTasksInfo.ActiveTaskCount}** active task{(activeTasksInfo.ActiveTaskCount is > 1 or 0 ? "s" : string.Empty)}\\.{Environment.NewLine}");

        foreach (var taskInfo in activeTasksInfo.TasksInfo)
        {
            sb.AppendLine(GetSingleTaskFormatted(taskInfo));
        }

        _logger.LogInformation(sb.ToString());
        await _bot.SendMessage(subscriber.TelegramId, sb.ToString(), parseMode: ParseMode.MarkdownV2);
    }

    public async Task ActivePullRequests(Subscriber subscriber, ActivePullRequestsInfo activePullRequestsInfo)
    {
        var sb = new StringBuilder();
        sb.Append($"ℹ You have **{activePullRequestsInfo.ActivePullRequestCount}** active pull request{(activePullRequestsInfo.ActivePullRequestCount is > 1 or 0 ? "s" : string.Empty)}\\.{Environment.NewLine}");

        foreach (var pr in activePullRequestsInfo.PullRequests)
        {
            sb.AppendLine(GetSinglePullRequestFormatted(pr));
        }

        _logger.LogInformation(sb.ToString());
        await _bot.SendMessage(subscriber.TelegramId, sb.ToString(), parseMode: ParseMode.MarkdownV2);
    }

    public async Task ActivePullRequestsReminder(Subscriber subscriber, ActivePullRequestsInfo activePullRequestsInfo)
    {
        if (!activePullRequestsInfo.HasActivePullRequests)
        {
            return;
        }

        var sb = new StringBuilder();
        sb.Append("🔔 *PR reminder\\!* 🔔\n");
        sb.Append($"You have **{activePullRequestsInfo.ActivePullRequestCount}** open pull request{(activePullRequestsInfo.ActivePullRequestCount is > 1 ? "s" : string.Empty)}\\.\n\n");

        foreach (var pr in activePullRequestsInfo.PullRequests)
        {
            sb.AppendLine(GetSinglePullRequestFormatted(pr));
        }

        _logger.LogInformation(sb.ToString());
        await _bot.SendMessage(subscriber.TelegramId, sb.ToString(), parseMode: ParseMode.MarkdownV2);
    }



    public async Task IncorrectEmail(long chatId)
    {
        await _bot.SendMessage(chatId, "Incorrect email address");
    }

    public async Task EmailUpdated(Subscriber subscriber)
    {
        var text = $"Email address is set to {subscriber.Email}, but is not yet verified.{Environment.NewLine}" +
                   "Please check you mailbox and send PIN to this chat.";
        await _bot.SendMessage(subscriber.TelegramId, text);
    }

    public async Task NoEmail(long chatId)
    {
        await _bot.SendMessage(chatId, "Your email is not set. Use /help command to fix it.");
    }

    public async Task AccountVerified(Subscriber subscriber)
    {
        await _bot.SendMessage(subscriber.TelegramId, "Your account is verified. Now you are able to request reports.");
    }

    public async Task CouldNotVerifyAccount(Subscriber subscriber)
    {
        await _bot.SendMessage(subscriber.TelegramId, "Your account could not be verified.");
    }

    public async Task Typing(long chatId, CancellationToken cancellationToken)
    {
        await _bot.SendChatAction(chatId, ChatAction.Typing, cancellationToken: cancellationToken);
    }

    public async Task RequestEmail(long chatId)
    {
        await _bot.SendMessage(chatId, RequestEmailMessage);
    }

    public async Task Respond(long chatId, string message)
    {
        await _bot.SendMessage(chatId, message);
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
                   $"Projects: {(subscriber.AzureDevOpsProjects == null || subscriber.AzureDevOpsProjects.Count == 0 ? "(all)" : string.Join(",", subscriber.AzureDevOpsProjects))}{Environment.NewLine}" +
                   $"Working hours (UTC): {subscriber.StartWorkingHoursUtc}-{subscriber.EndWorkingHoursUtc}{Environment.NewLine}" +
                   $"Is account verified: {subscriber.IsVerified}{Environment.NewLine}" +
                   $"Hours per day: {subscriber.HoursPerDay}{Environment.NewLine}" +
                   $"LastNoActiveTasksAlert: {subscriber.LastNoActiveTasksAlert}{Environment.NewLine}" +
                   $"LastMoreThanSingleTaskIsActiveAlert: {subscriber.LastMoreThanSingleTaskIsActiveAlert}{Environment.NewLine}" +
                   $"LastActiveTaskOutsideOfWorkingHoursAlert: {subscriber.LastActiveTaskOutsideOfWorkingHoursAlert}{Environment.NewLine}" +
                   $"*Time off:*\n```{timeOffTable}```"; // Usar texto pre-formateado con backticks

        // Enviar el mensaje con parse_mode Markdown para mantener el formato
        await _bot.SendMessage(subscriber.TelegramId, text, parseMode: ParseMode.MarkdownV2);
    }

    private string GetSingleTaskFormatted(TaskInfo taskInfo)
    {
        var taskTitle = taskInfo.Title.EscapeMarkdownV2();
        var activeTime = taskInfo.ActiveTime.ToString("0.##").EscapeMarkdownV2();
        var singleTaskFormatted = $@"• {GetSingleTaskLink(taskInfo, ParseMode.MarkdownV2)}: {taskTitle} \(Active: {activeTime} hs\)";

        if(taskInfo.Parent != null)
        {
            singleTaskFormatted += $"\nParent: {GetSingleTaskLink(taskInfo.Parent, ParseMode.MarkdownV2)}";
        }

        return singleTaskFormatted;
    }

    private static string GetSinglePullRequestFormatted(PullRequestInfo pr)
    {
        var title = (pr.Title ?? string.Empty).EscapeMarkdownV2();
        var repo = (pr.Repository ?? string.Empty).EscapeMarkdownV2();
        var project = (pr.Project ?? string.Empty).EscapeMarkdownV2();
        var url = pr.WebUrl;

        var linkText = $"PR {pr.Id}".EscapeMarkdownV2();
        var link = string.IsNullOrWhiteSpace(url) ? linkText : $"[{linkText}]({url})";

        var where = string.IsNullOrWhiteSpace(project) && string.IsNullOrWhiteSpace(repo)
            ? string.Empty
            : $" \\({project}/{repo}\\)";

        return $"• {link}: {title}{where}";
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