using HouseholdFinances.Domain.Entities;
using HouseholdFinances.Domain.Models;

namespace HouseholdFinances.Api.Models;

/// <summary>
/// Request body for creating or updating a bill. The bill is attributed to the current user and
/// scoped to the household named in the route; a household or user id is never accepted from the
/// client.
/// </summary>
/// <remarks>
/// <see cref="Interval"/> is nullable so the service can distinguish an omitted interval from the
/// zero-valued <see cref="Entities.Interval.Daily"/>; it is required only when
/// <see cref="Recurring"/> is true.
/// </remarks>
/// <param name="Name">The bill's label.</param>
/// <param name="Value">The bill's amount.</param>
/// <param name="StartDate">The first occurrence, inclusive.</param>
/// <param name="EndDate">The last occurrence, inclusive.</param>
/// <param name="Recurring">Whether the bill repeats.</param>
/// <param name="Interval">The cadence, required when <paramref name="Recurring"/> is true.</param>
public sealed record BillRequest(
    string? Name,
    double Value,
    DateTime StartDate,
    DateTime EndDate,
    bool Recurring,
    Interval? Interval)
{
    /// <summary>Maps the request onto the Bill domain's input model.</summary>
    /// <returns>The equivalent <see cref="BillInput"/>.</returns>
    public BillInput ToInput() => new(Name, Value, StartDate, EndDate, Recurring, Interval);
}
