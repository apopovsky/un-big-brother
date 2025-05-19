namespace UnTaskAlert.Common;

public static class HelperExtensions
{
    public static string EscapeMarkdownV2(this string text) =>
        text
            .Replace("_", "\\_")
            .Replace("*", "\\*")
            .Replace("[", "\\[")
            .Replace("]", "\\]")
            .Replace("(", "\\(")
            .Replace(")", "\\)")
            .Replace("~", "\\~")
            .Replace("`", "\\`")
            .Replace(">", "\\>")
            .Replace("#", "\\#")
            .Replace("+", "\\+")
            .Replace("-", "\\-")
            .Replace("=", "\\=")
            .Replace("|", "\\|")
            .Replace("{", "\\{")
            .Replace("}", "\\}")
            .Replace(".", "\\.")
            .Replace("!", "\\!");

    public static bool IsWeekend(this DateTime date) =>
        date.DayOfWeek == DayOfWeek.Saturday ||
        date.DayOfWeek == DayOfWeek.Sunday;

    public static bool IsWeekDay(this DateTime dateTime) => dateTime.DayOfWeek != DayOfWeek.Saturday && dateTime.DayOfWeek != DayOfWeek.Sunday;
}