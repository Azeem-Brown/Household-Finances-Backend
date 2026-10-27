using HouseholdFinances.Domain.Models;

namespace HouseholdFinances.Domain.Abstractions;

/// <summary>
/// Income operations for a household the current, authenticated user belongs to. The current user
/// is always resolved from <see cref="ICurrentUserService"/>; a user id is never accepted from the
/// client.
/// </summary>
/// <remarks>
/// <para>
/// <b>Household scoping.</b> An <c>Income</c> row carries the owning user's id and no household key
/// (a recorded decision), so a household's income is the set of entries owned by its members. The
/// household is named by the caller in the route and the service verifies membership; a household
/// the user does not belong to is reported as not found, so the API never discloses the existence
/// of another household's income.
/// </para>
/// <para>
/// <b>Attribution.</b> A created entry is attributed to the current user. The owning user of an
/// existing entry is immutable: an update never re-attributes a row.
/// </para>
/// <para>
/// <b>Recurrence.</b> Entries are persisted and returned raw. Occurrences are not expanded on the
/// server; the frontend expands them from <c>StartDate</c> by <c>Interval</c> until <c>EndDate</c>
/// (inclusive).
/// </para>
/// </remarks>
public interface IIncomeService
{
    /// <summary>
    /// Creates an income entry in the household, attributed to the current user, and returns it.
    /// </summary>
    /// <param name="householdId">The household the entry belongs to.</param>
    /// <param name="input">The entry's fields.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The created entry.</returns>
    /// <exception cref="Errors.HouseholdFinancesException">
    /// The input is invalid (<see cref="Errors.ErrorCode.InvalidInput"/>); the request is
    /// unauthenticated (<see cref="Errors.ErrorCode.Unauthorized"/>); or the current user is not a
    /// member of the household (<see cref="Errors.ErrorCode.NotFound"/>).
    /// </exception>
    Task<IncomeDetail> CreateAsync(
        Guid householdId,
        IncomeInput input,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the household's income entries (the entries owned by its members), ordered by start
    /// date and then name.
    /// </summary>
    /// <param name="householdId">The household whose entries are listed.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The household's income entries.</returns>
    /// <exception cref="Errors.HouseholdFinancesException">
    /// The request is unauthenticated (<see cref="Errors.ErrorCode.Unauthorized"/>) or the current
    /// user is not a member of the household (<see cref="Errors.ErrorCode.NotFound"/>).
    /// </exception>
    Task<IReadOnlyList<IncomeDetail>> ListAsync(
        Guid householdId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an income entry in the household and returns the updated entry. The owning user is not
    /// changed. A member of the household may update any entry the household owns.
    /// </summary>
    /// <param name="householdId">The household the entry belongs to.</param>
    /// <param name="incomeId">The entry's identifier.</param>
    /// <param name="input">The entry's replacement fields.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The updated entry.</returns>
    /// <exception cref="Errors.HouseholdFinancesException">
    /// The input is invalid (<see cref="Errors.ErrorCode.InvalidInput"/>); the request is
    /// unauthenticated (<see cref="Errors.ErrorCode.Unauthorized"/>); the current user is not a
    /// member of the household (<see cref="Errors.ErrorCode.NotFound"/>); or the entry does not
    /// belong to the household (<see cref="Errors.ErrorCode.NotFound"/>).
    /// </exception>
    Task<IncomeDetail> UpdateAsync(
        Guid householdId,
        Guid incomeId,
        IncomeInput input,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an income entry from the household. A member of the household may delete any entry
    /// the household owns.
    /// </summary>
    /// <param name="householdId">The household the entry belongs to.</param>
    /// <param name="incomeId">The entry's identifier.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <exception cref="Errors.HouseholdFinancesException">
    /// The request is unauthenticated (<see cref="Errors.ErrorCode.Unauthorized"/>); the current user
    /// is not a member of the household (<see cref="Errors.ErrorCode.NotFound"/>); or the entry does
    /// not belong to the household (<see cref="Errors.ErrorCode.NotFound"/>).
    /// </exception>
    Task DeleteAsync(
        Guid householdId,
        Guid incomeId,
        CancellationToken cancellationToken = default);
}
