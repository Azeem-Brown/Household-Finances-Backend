using HouseholdFinances.Domain.Entities;
using HouseholdFinances.Domain.Models;

namespace HouseholdFinances.Api.Models;

/// <summary>
/// Request body for creating or updating an income entry. The entry is attributed to the current
/// user and scoped to the household named in the route; a user id is never accepted from the client.
/// </summary>
/// <remarks>
/// <see cref="Interval"/> is nullable so the service can distinguish an omitted interval from the
/// zero-valued <see cref="Entities.Interval.Daily"/>; it is required only when
/// <see cref="Recurring"/> is true.
/// </remarks>
/// <param name="Name">The entry's label.</param>
/// <param name="Value">The entry's amount.</param>
/// <param name="StartDate">The first occurrence, inclusive.</param>
/// <param name="EndDate">The last occurrence, inclusive.</param>
/// <param name="Recurring">Whether the entry repeats.</param>
/// <param name="Interval">The cadence, required when <paramref name="Recurring"/> is true.</param>
public sealed record IncomeRequest(
    string? Name,
    double Value,
    DateTime StartDate,
    DateTime EndDate,
    bool Recurring,
    Interval? Interval)
{
    /// <summary>Maps the request onto the Income domain's input model.</summary>
    /// <returns>The equivalent <see cref="IncomeInput"/>.</returns>
    public IncomeInput ToInput() => new(Name, Value, StartDate, EndDate, Recurring, Interval);
}
