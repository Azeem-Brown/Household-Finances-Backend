using HouseholdFinances.Domain.Entities;
using HouseholdFinances.Domain.Models;

namespace HouseholdFinances.Tests.Domain;

/// <summary>
/// Verifies the occurrence rule shared with the frontend: step from StartDate by Interval until
/// EndDate inclusive, count a one-off once on its StartDate, ignore time of day, and stop when the
/// entry or the window ends.
/// </summary>
public class OccurrenceExpanderTests
{
    private static readonly PeriodWindow Q4 = new(new DateOnly(2026, 10, 1), new DateOnly(2026, 12, 31));
    private static readonly PeriodWindow October = new(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31));

    [Fact]
    public void Expand_ReturnsTheBiWeeklyOccurrencesInsideTheQuarter()
    {
        var occurrences = Expand(
            Utc(2026, 1, 5),
            Utc(2026, 12, 31),
            recurring: true,
            Interval.BiWeekly,
            Q4);

        Assert.Equal(
            new[]
            {
                new DateOnly(2026, 10, 12),
                new DateOnly(2026, 10, 26),
                new DateOnly(2026, 11, 9),
                new DateOnly(2026, 11, 23),
                new DateOnly(2026, 12, 7),
                new DateOnly(2026, 12, 21)
            },
            occurrences);
    }

    [Fact]
    public void Expand_ReturnsTheMonthlyOccurrencesInsideTheQuarter()
    {
        var occurrences = Expand(
            Utc(2026, 1, 20),
            Utc(2026, 12, 31),
            recurring: true,
            Interval.Monthly,
            Q4);

        Assert.Equal(
            new[]
            {
                new DateOnly(2026, 10, 20),
                new DateOnly(2026, 11, 20),
                new DateOnly(2026, 12, 20)
            },
            occurrences);
    }

    [Fact]
    public void Expand_StopsAYearlySeriesAfterItsOnlyOccurrence()
    {
        var occurrences = Expand(
            Utc(2026, 2, 1),
            Utc(2026, 12, 31),
            recurring: true,
            Interval.Yearly,
            Q4);

        Assert.Empty(occurrences);
    }

    [Fact]
    public void Expand_CountsAnOccurrenceExactlyOnEndDate()
    {
        var occurrences = Expand(
            Utc(2026, 1, 20),
            Utc(2026, 10, 20),
            recurring: true,
            Interval.Monthly,
            October);

        Assert.Equal(new[] { new DateOnly(2026, 10, 20) }, occurrences);
    }

    [Fact]
    public void Expand_StopsTheSeriesAfterEndDate()
    {
        var occurrences = Expand(
            Utc(2026, 1, 20),
            Utc(2026, 10, 20),
            recurring: true,
            Interval.Monthly,
            Q4);

        // November and December occurrences do not exist because the series ended on 2026-10-20.
        Assert.Equal(new[] { new DateOnly(2026, 10, 20) }, occurrences);
    }

    [Fact]
    public void Expand_AnchorsMonthlyStepsToTheOriginalDayOfMonth()
    {
        var window = new PeriodWindow(new DateOnly(2026, 2, 1), new DateOnly(2026, 3, 31));

        var occurrences = Expand(
            Utc(2026, 1, 31),
            Utc(2026, 12, 31),
            recurring: true,
            Interval.Monthly,
            window);

        Assert.Equal(
            new[]
            {
                new DateOnly(2026, 2, 28),
                new DateOnly(2026, 3, 31)
            },
            occurrences);
    }

    [Fact]
    public void Expand_CountsAOneOffOnceWhenItsStartDateIsInsideTheWindow()
    {
        var occurrences = Expand(
            Utc(2026, 10, 14),
            Utc(2026, 10, 14),
            recurring: false,
            interval: null,
            October);

        Assert.Equal(new[] { new DateOnly(2026, 10, 14) }, occurrences);
    }

    [Fact]
    public void Expand_DoesNotCountAOneOffOutsideTheWindow()
    {
        var occurrences = Expand(
            Utc(2026, 9, 30),
            Utc(2026, 9, 30),
            recurring: false,
            interval: null,
            October);

        Assert.Empty(occurrences);
    }

    [Fact]
    public void Expand_TreatsRecurringWithoutAnIntervalAsAOneOff()
    {
        var occurrences = Expand(
            Utc(2026, 10, 14),
            Utc(2026, 10, 14),
            recurring: true,
            interval: null,
            October);

        Assert.Equal(new[] { new DateOnly(2026, 10, 14) }, occurrences);
    }

    [Fact]
    public void Expand_TreatsNonRecurringWithAnIntervalAsAOneOff()
    {
        var occurrences = Expand(
            Utc(2026, 10, 14),
            Utc(2026, 12, 31),
            recurring: false,
            Interval.Monthly,
            Q4);

        Assert.Equal(new[] { new DateOnly(2026, 10, 14) }, occurrences);
    }

    [Fact]
    public void Expand_ReturnsNothingWhenEndDatePrecedesStartDate()
    {
        var occurrences = Expand(
            Utc(2026, 10, 14),
            Utc(2026, 10, 13),
            recurring: true,
            Interval.Monthly,
            October);

        Assert.Empty(occurrences);
    }

    [Fact]
    public void Expand_IgnoresTimeOfDayOnTheEntryDates()
    {
        var occurrences = Expand(
            new DateTime(2026, 10, 14, 23, 59, 0, DateTimeKind.Utc),
            new DateTime(2026, 10, 14, 0, 0, 0, DateTimeKind.Utc),
            recurring: false,
            interval: null,
            October);

        Assert.Equal(new[] { new DateOnly(2026, 10, 14) }, occurrences);
    }

    [Fact]
    public void Expand_ReturnsDailyAndWeeklyOccurrencesInsideTheWindow()
    {
        var daily = Expand(Utc(2026, 10, 1), Utc(2026, 10, 31), true, Interval.Daily, October);
        var weekly = Expand(Utc(2026, 9, 28), Utc(2026, 10, 31), true, Interval.Weekly, October);

        Assert.Equal(31, daily.Count);
        Assert.Equal(
            new[]
            {
                new DateOnly(2026, 10, 5),
                new DateOnly(2026, 10, 12),
                new DateOnly(2026, 10, 19),
                new DateOnly(2026, 10, 26)
            },
            weekly);
    }

    [Fact]
    public void Expand_DoesNotCountOccurrencesBeforeStartDate()
    {
        var occurrences = Expand(
            Utc(2026, 10, 20),
            Utc(2026, 12, 31),
            recurring: true,
            Interval.Monthly,
            October);

        Assert.Equal(new[] { new DateOnly(2026, 10, 20) }, occurrences);
    }

    private static IReadOnlyList<DateOnly> Expand(
        DateTime startDate,
        DateTime endDate,
        bool recurring,
        Interval? interval,
        PeriodWindow window) =>
        OccurrenceExpander.Expand(startDate, endDate, recurring, interval, window);

    private static DateTime Utc(int year, int month, int day) =>
        new(year, month, day, 0, 0, 0, DateTimeKind.Utc);
}
