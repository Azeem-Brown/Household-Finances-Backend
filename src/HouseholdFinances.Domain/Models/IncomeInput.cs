using HouseholdFinances.Domain.Entities;

namespace HouseholdFinances.Domain.Models;

/// <summary>
/// Input for creating or updating an income entry. The entry is attributed to the current user by
/// the service; a user id is never accepted from the client.
/// </summary>
/// <remarks>
/// <see cref="Interval"/> is nullable so the service can distinguish "no interval supplied" from the
/// zero-valued <see cref="Entities.Interval.Daily"/>. It is required only when
/// <see cref="Recurring"/> is true.
/// </remarks>
/// <param name="Name">The entry's label. Required, trimmed before storage.</param>
/// <param name="Value">The entry's amount. Must be non-negative.</param>
/// <param name="StartDate">The first occurrence, inclusive.</param>
/// <param name="EndDate">The last occurrence, inclusive. Must not be before <paramref name="StartDate"/>.</param>
/// <param name="Recurring">Whether the entry repeats.</param>
/// <param name="Interval">The cadence, required when <paramref name="Recurring"/> is true.</param>
public sealed record IncomeInput(
    string? Name,
    double Value,
    DateTime StartDate,
    DateTime EndDate,
    bool Recurring,
    Interval? Interval);
