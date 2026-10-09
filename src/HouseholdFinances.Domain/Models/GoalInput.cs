using HouseholdFinances.Domain.Entities;

namespace HouseholdFinances.Domain.Models;

/// <summary>
/// Input for creating or updating a goal. The goal is attributed to the current user and scoped to
/// the household named by the caller; a household or user id is never accepted from the client.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Interval"/> is nullable so the service can distinguish "no interval supplied" from the
/// zero-valued <see cref="Entities.Interval.Daily"/>. It is required only when
/// <see cref="Recurring"/> is true.
/// </para>
/// <para>
/// <see cref="Total"/> is the amount contributed toward the goal so far. It defaults to 0 when a goal
/// is created (the service forces this) and is changed only through the update operation.
/// </para>
/// </remarks>
/// <param name="Name">The goal's label. Required, trimmed before storage.</param>
/// <param name="Value">The goal's target amount. Must be non-negative.</param>
/// <param name="StartDate">The first occurrence, inclusive.</param>
/// <param name="EndDate">The last occurrence, inclusive. Must not be before <paramref name="StartDate"/>.</param>
/// <param name="Recurring">Whether the goal repeats.</param>
/// <param name="Interval">The cadence, required when <paramref name="Recurring"/> is true.</param>
/// <param name="Total">The amount contributed toward the goal so far.</param>
public sealed record GoalInput(
    string? Name,
    double Value,
    DateTime StartDate,
    DateTime EndDate,
    bool Recurring,
    Interval? Interval,
    double Total);
