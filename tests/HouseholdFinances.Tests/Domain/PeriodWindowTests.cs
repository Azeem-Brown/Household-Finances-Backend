using System.Globalization;
using HouseholdFinances.Domain.Entities;
using HouseholdFinances.Domain.Models;

namespace HouseholdFinances.Tests.Domain;

/// <summary>
/// Verifies the six calendar-aligned period windows. The reference date 2026-10-14 is a Wednesday,
/// matching the shared fixture from the issue.
/// </summary>
public class PeriodWindowTests
{
    private static readonly DateOnly Wednesday = new(2026, 10, 14);

    [Theory]
    [InlineData(Interval.Daily, "2026-10-14", "2026-10-14")]
    [InlineData(Interval.Weekly, "2026-10-12", "2026-10-18")]
    [InlineData(Interval.BiWeekly, "2026-10-12", "2026-10-25")]
    [InlineData(Interval.Monthly, "2026-10-01", "2026-10-31")]
    [InlineData(Interval.Quarterly, "2026-10-01", "2026-12-31")]
    [InlineData(Interval.Yearly, "2026-01-01", "2026-12-31")]
    public void For_ReturnsTheCalendarAlignedWindowContainingToday(
        Interval period,
        string expectedStart,
        string expectedEnd)
    {
        var window = PeriodWindow.For(period, Wednesday);

        Assert.Equal(Date(expectedStart), window.Start);
        Assert.Equal(Date(expectedEnd), window.End);
    }

    [Fact]
    public void For_WeeklyOnAMonday_StartsThatMonday()
    {
        var window = PeriodWindow.For(Interval.Weekly, new DateOnly(2026, 10, 12));

        Assert.Equal(new DateOnly(2026, 10, 12), window.Start);
        Assert.Equal(new DateOnly(2026, 10, 18), window.End);
    }

    [Fact]
    public void For_WeeklyOnASunday_IsStillTheWeekBeginningThePreviousMonday()
    {
        var window = PeriodWindow.For(Interval.Weekly, new DateOnly(2026, 10, 18));

        Assert.Equal(new DateOnly(2026, 10, 12), window.Start);
        Assert.Equal(new DateOnly(2026, 10, 18), window.End);
    }

    [Fact]
    public void For_MonthlyInALeapYear_UsesTheCalendarMonthLength()
    {
        var window = PeriodWindow.For(Interval.Monthly, new DateOnly(2028, 2, 15));

        Assert.Equal(new DateOnly(2028, 2, 1), window.Start);
        Assert.Equal(new DateOnly(2028, 2, 29), window.End);
    }

    [Theory]
    [InlineData(2, 15, "2026-01-01", "2026-03-31")]
    [InlineData(6, 30, "2026-04-01", "2026-06-30")]
    [InlineData(12, 31, "2026-10-01", "2026-12-31")]
    public void For_Quarterly_UsesTheCalendarQuarter(
        int month,
        int day,
        string expectedStart,
        string expectedEnd)
    {
        var window = PeriodWindow.For(Interval.Quarterly, new DateOnly(2026, month, day));

        Assert.Equal(Date(expectedStart), window.Start);
        Assert.Equal(Date(expectedEnd), window.End);
    }

    [Fact]
    public void Contains_IncludesBothBounds()
    {
        var window = new PeriodWindow(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31));

        Assert.True(window.Contains(new DateOnly(2026, 10, 1)));
        Assert.True(window.Contains(new DateOnly(2026, 10, 31)));
        Assert.False(window.Contains(new DateOnly(2026, 9, 30)));
        Assert.False(window.Contains(new DateOnly(2026, 11, 1)));
    }

    [Fact]
    public void For_RejectsAnUndefinedIntervalValue()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PeriodWindow.For((Interval)99, Wednesday));
    }

    private static DateOnly Date(string value) =>
        DateOnly.ParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture);
}
