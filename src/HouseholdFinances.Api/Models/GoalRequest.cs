using HouseholdFinances.Domain.Entities;
using HouseholdFinances.Domain.Models;

namespace HouseholdFinances.Api.Models;

/// <summary>
/// Request body for creating or updating a goal. The goal is attributed to the current user and
/// scoped to the household named in the route; a household or user id is never accepted from the
/// client.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Interval"/> is nullable so the service can distinguish an omitted interval from the
/// zero-valued <see cref="Entities.Interval.Daily"/>; it is required only when
/// <see cref="Recurring"/> is true. A supplied interval on a non-recurring goal is cleared to null
/// rather than rejected, so the response always reports the one-off representation.
/// </para>
/// <para>
/// <b>Recurrence</b> (approved decision #5). A recurring goal expands occurrences from
/// <see cref="StartDate"/> by <see cref="Interval"/> until <see cref="EndDate"/> inclusive, stepping
/// +1 day, +7 days, +14 days, +1 month, +3 months, or +1 year, anchored to the original start date.
/// A goal that is not recurring has exactly one occurrence, on <see cref="StartDate"/>. Dates are
/// UTC calendar dates, so time of day is ignored.
/// </para>
/// <para>
/// <see cref="Value"/> is the target amount and is never multiplied by the number of occurrences.
/// <see cref="Total"/> is the amount contributed toward the goal so far. A create ignores it (a new
/// goal starts at 0); an update applies it.
/// </para>
/// </remarks>
/// <param name="Name">The goal's label.</param>
/// <param name="Value">The goal's target amount.</param>
/// <param name="StartDate">The first occurrence, inclusive.</param>
/// <param name="EndDate">The last occurrence, inclusive.</param>
/// <param name="Recurring">Whether the goal repeats.</param>
/// <param name="Interval">The cadence, required when <paramref name="Recurring"/> is true.</param>
/// <param name="Total">The amount contributed toward the goal so far.</param>
public sealed record GoalRequest(
    string? Name,
    double Value,
    DateTime StartDate,
    DateTime EndDate,
    bool Recurring,
    Interval? Interval,
    double Total)
{
    /// <summary>Maps the request onto the Goal domain's input model.</summary>
    /// <returns>The equivalent <see cref="GoalInput"/>.</returns>
    public GoalInput ToInput() => new(Name, Value, StartDate, EndDate, Recurring, Interval, Total);
}
