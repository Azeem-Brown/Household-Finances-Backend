using HouseholdFinances.Domain.Entities;

namespace HouseholdFinances.Domain.Models;

/// <summary>
/// Expands a single Income or Bill entry into the discrete occurrence dates that fall inside a
/// <see cref="PeriodWindow"/>. It is a pure calendar calculation with no EF Core types, so it can
/// be unit tested directly and reused for both entry kinds.
/// </summary>
/// <remarks>
/// <para>
/// Recurrence follows the approved rule: occurrences are the entry's <c>StartDate</c> and then
/// <c>StartDate</c> stepped by one <c>Interval</c> at a time, while the occurrence is on or before
/// <c>EndDate</c> (inclusive). Calendar steps are anchored to the original <c>StartDate</c>, so a
/// series starting on 31 January steps to the end of February and back to 31 March.
/// </para>
/// <para>
/// An entry is recurring only when it is marked recurring and has an interval. Every other
/// combination is a one-off: <c>Recurring == true</c> with no interval, and
/// <c>Recurring == false</c> with an interval, both count once on <c>StartDate</c>.
/// </para>
/// <para>
/// Only calendar dates are compared; time of day on the dates and the window is ignored.
/// </para>
/// </remarks>
public static class OccurrenceExpander
{
    /// <summary>
    /// Returns the occurrence dates of an entry that fall inside <paramref name="window"/>, in
    /// ascending order. Empty when the entry has no occurrence in the window, including when
    /// <paramref name="endDate"/> precedes <paramref name="startDate"/>.
    /// </summary>
    /// <param name="startDate">The entry's start date.</param>
    /// <param name="endDate">The entry's inclusive end date.</param>
    /// <param name="recurring">Whether the entry is marked recurring.</param>
    /// <param name="interval">The entry's cadence, or <c>null</c> for a one-off.</param>
    /// <param name="window">The inclusive window to expand into.</param>
    /// <returns>The occurrence dates inside the window.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="interval"/> is not one of the defined <see cref="Interval"/> members.
    /// </exception>
    public static IReadOnlyList<DateOnly> Expand(
        DateTime startDate,
        DateTime endDate,
        bool recurring,
        Interval? interval,
        PeriodWindow window)
    {
        DateOnly start = DateOnly.FromDateTime(startDate);
        DateOnly end = DateOnly.FromDateTime(endDate);

        // An entry whose EndDate precedes its StartDate produces no occurrences, even when its
        // StartDate lies inside the window.
        if (end < start)
        {
            return Array.Empty<DateOnly>();
        }

        // Recurring requires both the flag and a cadence. Everything else is a one-off, which
        // counts once when its StartDate falls inside the window.
        if (!recurring || interval is null)
        {
            return window.Contains(start) ? new[] { start } : Array.Empty<DateOnly>();
        }

        return ExpandRecurring(start, end, interval.Value, window);
    }

    private static IReadOnlyList<DateOnly> ExpandRecurring(
        DateOnly start,
        DateOnly end,
        Interval interval,
        PeriodWindow window)
    {
        List<DateOnly> occurrences = new List<DateOnly>();

        // Jump straight to the first occurrence at or after the window start. The series is never
        // walked one day at a time from the original StartDate.
        for (int index = FirstIndexAtOrAfter(start, interval, window.Start); ; index++)
        {
            DateOnly occurrence = OccurrenceAt(start, interval, index);

            // The series stops at EndDate. The window end is a second, independent stop.
            if (occurrence > end || occurrence > window.End)
            {
                break;
            }

            // FirstIndexAtOrAfter guarantees this, but the guard keeps the invariant explicit if
            // the index calculation is ever changed.
            if (occurrence >= window.Start)
            {
                occurrences.Add(occurrence);
            }
        }

        return occurrences;
    }

    private static DateOnly OccurrenceAt(DateOnly start, Interval interval, int index) => interval switch
    {
        Interval.Daily => start.AddDays(index),
        Interval.Weekly => start.AddDays(7 * index),
        Interval.BiWeekly => start.AddDays(14 * index),
        Interval.Monthly => start.AddMonths(index),
        Interval.Quarterly => start.AddMonths(3 * index),
        Interval.Yearly => start.AddMonths(12 * index),
        _ => throw new ArgumentOutOfRangeException(nameof(interval), interval, "Unknown interval.")
    };

    private static int FirstIndexAtOrAfter(DateOnly start, Interval interval, DateOnly windowStart)
    {
        if (windowStart <= start)
        {
            return 0;
        }

        switch (interval)
        {
            case Interval.Daily:
                return windowStart.DayNumber - start.DayNumber;
            case Interval.Weekly:
                return DivideRoundingUp(windowStart.DayNumber - start.DayNumber, 7);
            case Interval.BiWeekly:
                return DivideRoundingUp(windowStart.DayNumber - start.DayNumber, 14);
            case Interval.Monthly:
                return FirstCalendarIndexAtOrAfter(start, windowStart, interval, stepMonths: 1);
            case Interval.Quarterly:
                return FirstCalendarIndexAtOrAfter(start, windowStart, interval, stepMonths: 3);
            case Interval.Yearly:
                return FirstCalendarIndexAtOrAfter(start, windowStart, interval, stepMonths: 12);
            default:
                throw new ArgumentOutOfRangeException(nameof(interval), interval, "Unknown interval.");
        }
    }

    private static int FirstCalendarIndexAtOrAfter(
        DateOnly start,
        DateOnly windowStart,
        Interval interval,
        int stepMonths)
    {
        int monthsBetween = ((windowStart.Year - start.Year) * 12) + (windowStart.Month - start.Month);
        int index = Math.Max(0, monthsBetween / stepMonths);

        // The day-of-month is anchored to StartDate, so the estimated occurrence can still precede
        // the window start inside the same month (for example StartDate 10 January and a window
        // starting 15 March). Advance to the next step when that happens.
        while (OccurrenceAt(start, interval, index) < windowStart)
        {
            index++;
        }

        return index;
    }

    private static int DivideRoundingUp(int dividend, int divisor) => (dividend + divisor - 1) / divisor;
}
