using HouseholdFinances.Domain.Entities;

namespace HouseholdFinances.Domain.Models;

/// <summary>
/// Inclusive calendar-date window used when totalling a household's entry occurrences. Both bounds
/// are UTC calendar dates and both are inclusive: an occurrence on <see cref="Start"/> or
/// <see cref="End"/> falls inside the window.
/// </summary>
/// <param name="Start">The first date inside the window.</param>
/// <param name="End">The last date inside the window.</param>
public readonly record struct PeriodWindow(DateOnly Start, DateOnly End)
{
    /// <summary>
    /// Creates the calendar-aligned window for <paramref name="period"/> that contains
    /// <paramref name="today"/>:
    /// <list type="bullet">
    /// <item><description>Daily: the current day.</description></item>
    /// <item><description>Weekly: the current Monday-to-Sunday week.</description></item>
    /// <item><description>BiWeekly: the 14-day window beginning on the Monday of the current week.</description></item>
    /// <item><description>Monthly: the current calendar month.</description></item>
    /// <item><description>Quarterly: the current calendar quarter.</description></item>
    /// <item><description>Yearly: the current calendar year.</description></item>
    /// </list>
    /// </summary>
    /// <param name="period">The cadence whose calendar window is requested.</param>
    /// <param name="today">The UTC calendar date treated as "now".</param>
    /// <returns>The inclusive window.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="period"/> is not one of the defined <see cref="Interval"/> members.
    /// </exception>
    public static PeriodWindow For(Interval period, DateOnly today) => period switch
    {
        Interval.Daily => new PeriodWindow(today, today),
        Interval.Weekly => WeekBeginningOn(today),
        Interval.BiWeekly => BiWeeklyContaining(today),
        Interval.Monthly => MonthContaining(today),
        Interval.Quarterly => QuarterContaining(today),
        Interval.Yearly => new PeriodWindow(
            new DateOnly(today.Year, 1, 1),
            new DateOnly(today.Year, 12, 31)),
        _ => throw new ArgumentOutOfRangeException(nameof(period), period, "Unknown interval.")
    };

    /// <summary>Reports whether <paramref name="date"/> falls inside the window, bounds included.</summary>
    /// <param name="date">The calendar date to test.</param>
    /// <returns><c>true</c> when the date is inside the window; otherwise <c>false</c>.</returns>
    public bool Contains(DateOnly date) => date >= Start && date <= End;

    // The current week runs Monday to Sunday.
    private static PeriodWindow WeekBeginningOn(DateOnly today)
    {
        int daysSinceMonday = ((int)today.DayOfWeek + 6) % 7;
        DateOnly monday = today.AddDays(-daysSinceMonday);

        return new PeriodWindow(monday, monday.AddDays(6));
    }

    // BiWeekly is anchored to the Monday of the current week, not to an arbitrary epoch.
    private static PeriodWindow BiWeeklyContaining(DateOnly today)
    {
        DateOnly monday = WeekBeginningOn(today).Start;

        return new PeriodWindow(monday, monday.AddDays(13));
    }

    private static PeriodWindow MonthContaining(DateOnly today)
    {
        DateOnly first = new DateOnly(today.Year, today.Month, 1);
        DateOnly last = new DateOnly(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month));

        return new PeriodWindow(first, last);
    }

    private static PeriodWindow QuarterContaining(DateOnly today)
    {
        int firstMonth = ((today.Month - 1) / 3) * 3 + 1;
        DateOnly first = new DateOnly(today.Year, firstMonth, 1);
        DateOnly last = first.AddMonths(3).AddDays(-1);

        return new PeriodWindow(first, last);
    }
}
