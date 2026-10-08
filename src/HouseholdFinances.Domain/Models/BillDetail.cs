using HouseholdFinances.Domain.Entities;

namespace HouseholdFinances.Domain.Models;

/// <summary>
/// Read model returned for a bill. It is the raw stored entry: recurrence is never expanded on the
/// server (the frontend expands occurrences and computes totals). Money is USD with two decimal
/// places, half-up, applied when the entry is written.
/// </summary>
/// <remarks>
/// A bill carries its household key directly (unlike income, which reaches a household through the
/// owning user's membership), because bills belong to a household rather than to a person.
/// </remarks>
/// <param name="Id">The bill's identifier.</param>
/// <param name="Name">The bill's label.</param>
/// <param name="Value">The bill's amount.</param>
/// <param name="StartDate">The first occurrence, inclusive.</param>
/// <param name="EndDate">The last occurrence, inclusive.</param>
/// <param name="Recurring">Whether the bill repeats.</param>
/// <param name="Interval">
/// The cadence used when <paramref name="Recurring"/> is true; <see langword="null"/> for a
/// one-off bill.
/// </param>
/// <param name="HouseholdId">The household the bill belongs to.</param>
/// <param name="UserId">The user the bill is attributed to (the member who created it).</param>
public sealed record BillDetail(
    Guid Id,
    string Name,
    double Value,
    DateTime StartDate,
    DateTime EndDate,
    bool Recurring,
    Interval? Interval,
    Guid HouseholdId,
    Guid UserId);
