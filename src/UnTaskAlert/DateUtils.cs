namespace UnTaskAlert
{
    public class DateUtils
    {
        public static DateTime StartOfWeek()
        {
            var dt = DateTime.Today;
            int diff = (7 + (dt.DayOfWeek - DayOfWeek.Monday)) % 7;

            return dt.AddDays(-1 * diff).Date;
        }

        public static DateTime StartOfMonth()
        {
            return new DateTime(DateTime.Today.Date.Year, DateTime.UtcNow.Date.Month, 1);
        }
    }
}
