namespace UnTaskAlert;

public static class DateUtils
{
    public static readonly string[] AcceptedDateInputFormats =
    [
        "yyyy.MM.dd",
        "yyyyMMdd",
        "dd.MM.yyyy",
        "dd/MM/yyyy",
        "dd-MM-yyyy",
        "yyyy-MM-dd",
    ];

    public static DateTime StartOfWeek()
    {
        var dt = DateTime.Today;
        var diff = (7 + (dt.DayOfWeek - DayOfWeek.Monday)) % 7;

        return dt.AddDays(-1 * diff).Date;
    }

    public static DateTime StartOfMonth() => new(DateTime.Today.Date.Year, DateTime.UtcNow.Date.Month, 1);
}