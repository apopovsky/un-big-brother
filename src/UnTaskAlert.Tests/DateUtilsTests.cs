using System;

namespace UnTaskAlert.Tests;

[TestClass]
public sealed class DateUtilsTests
{
    [TestMethod]
    public void StartOfWeek_ReturnsMonday()
    {
        var start = DateUtils.StartOfWeek();

        Assert.AreEqual(DayOfWeek.Monday, start.DayOfWeek);
        Assert.AreEqual(start.Date, start);
        Assert.IsTrue(start <= DateTime.Today);
        Assert.IsTrue((DateTime.Today - start).TotalDays < 7);
    }

    [TestMethod]
    public void StartOfMonth_IsFirstDayOfUtcMonthUsingCurrentImplementation()
    {
        var start = DateUtils.StartOfMonth();
        var expected = new DateTime(DateTime.Today.Date.Year, DateTime.UtcNow.Date.Month, 1);

        Assert.AreEqual(expected, start);
    }
}
