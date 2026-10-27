using HouseholdFinances.Domain.Entities;

namespace HouseholdFinances.Domain.Models;

/// <summary>
/// Read model returned for a goal. It is the raw stored entry: recurrence is never expanded on the
/// server (the frontend expands occurrences and computes the time-to-goal projection). Money is USD
/// with two decimal places, half-up, applied when the entry is written.
/// </summary>
/// <remarks>
/// <para>
/// A goal belongs to a household and records the user who created it, so it carries both
/// <paramref name="HouseholdId"/> and <paramref name="UserId"/>.
/// </para>
/// <para>
/// <b>Recurrence</b> (approved decision #5). A recurring goal's occurrences are
/// <paramref name="StartDate"/>, then <paramref name="StartDate"/> stepped by
/// <paramref name="Interval"/> one at a time while the occurrence is on or before
/// <paramref name="EndDate"/>; <paramref name="EndDate"/> is inclusive. Stepping is +1 day, +7 days,
/// +14 days, +1 month, +3 months, or +1 year, anchored to the original start date. Dates are compared
/// as UTC calendar dates, so time of day is ignored. A goal that is not recurring has exactly one
/// occurrence, on <paramref name="StartDate"/>.
/// </para>
/// <para>
/// <b>Money.</b> <paramref name="Value"/> is the target amount and is never multiplied by the number
/// of occurrences. <paramref name="Total"/> is the amount contributed so far and does not depend on
/// the occurrence dates. Goals do not contribute to a household's incomes or payments.
/// </para>
/// </remarks>
/// <param name="Id">The goal's identifier.</param>
/// <param name="Name">The goal's label.</param>
/// <param name="Value">The goal's target amount.</param>
/// <param name="StartDate">The first occurrence, inclusive.</param>
/// <param name="EndDate">The last occurrence, inclusive.</param>
/// <param name="Recurring">Whether the goal repeats.</param>
/// <param name="Interval">
/// The cadence used when <paramref name="Recurring"/> is true; <see langword="null"/> for a one-off
/// goal. The service clears a supplied cadence on a non-recurring goal, so this is null whenever
/// <paramref name="Recurring"/> is false.
/// </param>
/// <param name="HouseholdId">The household the goal belongs to.</param>
/// <param name="UserId">The user the goal is attributed to (the member who created it).</param>
/// <param name="Total">The amount contributed toward the goal so far.</param>
public sealed record GoalDetail(
    Guid Id,
    string Name,
    double Value,
    DateTime StartDate,
    DateTime EndDate,
    bool Recurring,
    Interval? Interval,
    Guid HouseholdId,
    Guid UserId,
    double Total);
