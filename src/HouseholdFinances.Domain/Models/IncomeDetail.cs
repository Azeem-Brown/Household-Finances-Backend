using HouseholdFinances.Domain.Entities;

namespace HouseholdFinances.Domain.Models;

/// <summary>
/// Read model returned for an income entry. It is the raw stored entry: recurrence is never
/// expanded on the server (the frontend expands occurrences and computes totals). Money is USD with
/// two decimal places, half-up, applied when the entry is written.
/// </summary>
/// <param name="Id">The income entry's identifier.</param>
/// <param name="Name">The entry's label.</param>
/// <param name="Value">The entry's amount.</param>
/// <param name="StartDate">The first occurrence, inclusive.</param>
/// <param name="EndDate">The last occurrence, inclusive.</param>
/// <param name="Recurring">Whether the entry repeats.</param>
/// <param name="Interval">The cadence used when <paramref name="Recurring"/> is true.</param>
/// <param name="UserId">The user the entry is attributed to (its owner).</param>
public sealed record IncomeDetail(
    Guid Id,
    string Name,
    double Value,
    DateTime StartDate,
    DateTime EndDate,
    bool Recurring,
    Interval Interval,
    Guid UserId);
