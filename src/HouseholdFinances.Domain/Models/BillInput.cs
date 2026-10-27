using HouseholdFinances.Domain.Entities;

namespace HouseholdFinances.Domain.Models;

/// <summary>
/// Input for creating or updating a bill. The bill is attributed to the current user and scoped to
/// the household named by the caller; a household or user id is never accepted from the client.
/// </summary>
/// <remarks>
/// <see cref="Interval"/> is nullable so the service can distinguish "no interval supplied" from the
/// zero-valued <see cref="Entities.Interval.Daily"/>. It is required only when
/// <see cref="Recurring"/> is true.
/// </remarks>
/// <param name="Name">The bill's label. Required, trimmed before storage.</param>
/// <param name="Value">The bill's amount. Must be non-negative.</param>
/// <param name="StartDate">The first occurrence, inclusive.</param>
/// <param name="EndDate">The last occurrence, inclusive. Must not be before <paramref name="StartDate"/>.</param>
/// <param name="Recurring">Whether the bill repeats.</param>
/// <param name="Interval">The cadence, required when <paramref name="Recurring"/> is true.</param>
public sealed record BillInput(
    string? Name,
    double Value,
    DateTime StartDate,
    DateTime EndDate,
    bool Recurring,
    Interval? Interval);
