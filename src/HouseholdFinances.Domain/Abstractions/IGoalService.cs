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
/// server; the frontend expands them from <c>StartDate</c> by <c>Interval</c> until <c>EndDate</c>
/// (inclusive) and computes the projection.
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
