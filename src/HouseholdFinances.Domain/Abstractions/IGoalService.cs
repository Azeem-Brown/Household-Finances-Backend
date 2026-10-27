using HouseholdFinances.Domain.Models;

namespace HouseholdFinances.Domain.Abstractions;

/// <summary>
/// Goal operations for a household the current, authenticated user belongs to. The current user is
/// always resolved from <see cref="ICurrentUserService"/>; a user id is never accepted from the
/// client.
/// </summary>
/// <remarks>
/// <para>
/// <b>Household scoping.</b> A <c>Goal</c> row carries the household key directly and is attributed
/// to the creating user (a recorded decision). A goal is therefore reached both through its
/// household and through its owner, so only members of the household may read or modify its goals.
/// The household is named by the caller in the route and the service verifies membership; a
/// household the user does not belong to is reported as not found, so the API never discloses the
/// existence of another household's goals.
/// </para>
/// <para>
/// <b>Attribution.</b> A created goal is attributed to the current user. The owning user and the
/// household of an existing goal are immutable: an update never re-attributes or moves a row.
/// </para>
/// <para>
/// <b>Recurrence.</b> Goals are persisted and returned raw. Occurrences are not expanded on the
/// server; the frontend expands them (see the occurrence helper on frontend issue #49) from
/// <c>StartDate</c> by <c>Interval</c> until <c>EndDate</c> (inclusive) and computes the projection.
/// The rule is the one approved for Income, Bills, and Goals by decision #5: occurrences are
/// <c>StartDate</c>, then <c>StartDate</c> stepped by one interval at a time while the occurrence is
/// on or before <c>EndDate</c>; stepping is +1 day, +7 days, +14 days, +1 month, +3 months, or +1
/// year, anchored to the original <c>StartDate</c>; dates are UTC calendar dates, so time of day is
/// ignored; an entry whose <c>EndDate</c> precedes its <c>StartDate</c> produces no occurrences.
/// </para>
/// <para>
/// <b>Storage.</b> A goal has exactly one representation per kind: a recurring goal stores its
/// cadence, and a one-off goal stores no cadence. The service clears a supplied interval to null
/// whenever the goal is not recurring, and rejects a recurring goal without one. A one-off goal
/// therefore has exactly one occurrence, on <c>StartDate</c>.
/// </para>
/// <para>
/// <b>Money.</b> <c>Value</c> is the goal's target amount and is never multiplied by the number of
/// occurrences. <c>Total</c> is the amount contributed so far and does not depend on occurrence
/// dates. Goals do not contribute to a household's incomes or payments.
/// </para>
/// </remarks>
public interface IGoalService
{
    /// <summary>
    /// Creates a goal in the household, attributed to the current user, with a contribution total of
    /// zero, and returns it.
    /// </summary>
    /// <param name="householdId">The household the goal belongs to.</param>
    /// <param name="input">The goal's fields.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The created goal.</returns>
    /// <exception cref="Errors.HouseholdFinancesException">
    /// The input is invalid (<see cref="Errors.ErrorCode.InvalidInput"/>); the request is
    /// unauthenticated (<see cref="Errors.ErrorCode.Unauthorized"/>); or the current user is not a
    /// member of the household (<see cref="Errors.ErrorCode.NotFound"/>).
    /// </exception>
    Task<GoalDetail> CreateAsync(
        Guid householdId,
        GoalInput input,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the household's goals, ordered by start date and then name.
    /// </summary>
    /// <param name="householdId">The household whose goals are listed.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The household's goals.</returns>
    /// <exception cref="Errors.HouseholdFinancesException">
    /// The request is unauthenticated (<see cref="Errors.ErrorCode.Unauthorized"/>) or the current
    /// user is not a member of the household (<see cref="Errors.ErrorCode.NotFound"/>).
    /// </exception>
    Task<IReadOnlyList<GoalDetail>> ListAsync(
        Guid householdId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a goal in the household and returns the updated goal. The owning user and the household
    /// are not changed. The contribution total is applied from the input. A member of the household
    /// may update any goal the household owns.
    /// </summary>
    /// <param name="householdId">The household the goal belongs to.</param>
    /// <param name="goalId">The goal's identifier.</param>
    /// <param name="input">The goal's replacement fields.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The updated goal.</returns>
    /// <exception cref="Errors.HouseholdFinancesException">
    /// The input is invalid (<see cref="Errors.ErrorCode.InvalidInput"/>); the request is
    /// unauthenticated (<see cref="Errors.ErrorCode.Unauthorized"/>); the current user is not a
    /// member of the household (<see cref="Errors.ErrorCode.NotFound"/>); or the goal does not
    /// belong to the household (<see cref="Errors.ErrorCode.NotFound"/>).
    /// </exception>
    Task<GoalDetail> UpdateAsync(
        Guid householdId,
        Guid goalId,
        GoalInput input,
        CancellationToken cancellationToken = default);
}
