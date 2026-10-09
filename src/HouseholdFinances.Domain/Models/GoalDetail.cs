using HouseholdFinances.Domain.Entities;

namespace HouseholdFinances.Domain.Models;

/// <summary>
/// Read model returned for a goal. It is the raw stored entry: recurrence is never expanded on the
/// server (the frontend expands occurrences and computes the time-to-goal projection). Money is USD
/// with two decimal places, half-up, applied when the entry is written.
/// </summary>
/// <remarks>
/// A goal belongs to a household and records the user who created it, so it carries both
/// <paramref name="HouseholdId"/> and <paramref name="UserId"/>.
/// </remarks>
/// <param name="Id">The goal's identifier.</param>
/// <param name="Name">The goal's label.</param>
/// <param name="Value">The goal's target amount.</param>
/// <param name="StartDate">The first occurrence, inclusive.</param>
/// <param name="EndDate">The last occurrence, inclusive.</param>
/// <param name="Recurring">Whether the goal repeats.</param>
/// <param name="Interval">
/// The cadence used when <paramref name="Recurring"/> is true; <see langword="null"/> for a one-off
/// goal.
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
