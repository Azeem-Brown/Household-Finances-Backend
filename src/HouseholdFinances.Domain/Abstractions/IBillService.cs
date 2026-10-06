using HouseholdFinances.Domain.Models;

namespace HouseholdFinances.Domain.Abstractions;

/// <summary>
/// Bill operations for a household the current, authenticated user belongs to. The current user is
/// always resolved from <see cref="ICurrentUserService"/>; a user id is never accepted from the
/// client.
/// </summary>
/// <remarks>
/// <para>
/// <b>Household scoping.</b> A <c>Bill</c> row carries the household key directly (a recorded
/// decision), so a household's bills are the rows tagged with that household. The household is
/// named by the caller in the route and the service verifies membership; a household the user does
/// not belong to is reported as not found, so the API never discloses the existence of another
/// household's bills.
/// </para>
/// <para>
/// <b>Attribution.</b> A created bill is attributed to the current user. The creating user of an
/// existing bill is immutable: an update never re-attributes a row.
/// </para>
/// <para>
/// <b>Recurrence.</b> Bills are persisted and returned raw. Occurrences are not expanded on the
/// server; the frontend expands them from <c>StartDate</c> by <c>Interval</c> until <c>EndDate</c>
/// (inclusive).
/// </para>
/// </remarks>
public interface IBillService
{
    /// <summary>
    /// Creates a bill in the household, attributed to the current user, and returns it.
    /// </summary>
    /// <param name="householdId">The household the bill belongs to.</param>
    /// <param name="input">The bill's fields.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The created bill.</returns>
    /// <exception cref="Errors.HouseholdFinancesException">
    /// The input is invalid (<see cref="Errors.ErrorCode.InvalidInput"/>); the request is
    /// unauthenticated (<see cref="Errors.ErrorCode.Unauthorized"/>); or the current user is not a
    /// member of the household (<see cref="Errors.ErrorCode.NotFound"/>).
    /// </exception>
    Task<BillDetail> CreateAsync(
        Guid householdId,
        BillInput input,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the household's bills, ordered by start date and then name.
    /// </summary>
    /// <param name="householdId">The household whose bills are listed.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The household's bills.</returns>
    /// <exception cref="Errors.HouseholdFinancesException">
    /// The request is unauthenticated (<see cref="Errors.ErrorCode.Unauthorized"/>) or the current
    /// user is not a member of the household (<see cref="Errors.ErrorCode.NotFound"/>).
    /// </exception>
    Task<IReadOnlyList<BillDetail>> ListAsync(
        Guid householdId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a bill in the household and returns the updated bill. The creating user is not
    /// changed. A member of the household may update any bill the household owns.
    /// </summary>
    /// <param name="householdId">The household the bill belongs to.</param>
    /// <param name="billId">The bill's identifier.</param>
    /// <param name="input">The bill's replacement fields.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The updated bill.</returns>
    /// <exception cref="Errors.HouseholdFinancesException">
    /// The input is invalid (<see cref="Errors.ErrorCode.InvalidInput"/>); the request is
    /// unauthenticated (<see cref="Errors.ErrorCode.Unauthorized"/>); the current user is not a
    /// member of the household (<see cref="Errors.ErrorCode.NotFound"/>); or the bill does not
    /// belong to the household (<see cref="Errors.ErrorCode.NotFound"/>).
    /// </exception>
    Task<BillDetail> UpdateAsync(
        Guid householdId,
        Guid billId,
        BillInput input,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a bill from the household. A member of the household may delete any bill the
    /// household owns.
    /// </summary>
    /// <param name="householdId">The household the bill belongs to.</param>
    /// <param name="billId">The bill's identifier.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <exception cref="Errors.HouseholdFinancesException">
    /// The request is unauthenticated (<see cref="Errors.ErrorCode.Unauthorized"/>); the current user
    /// is not a member of the household (<see cref="Errors.ErrorCode.NotFound"/>); or the bill does
    /// not belong to the household (<see cref="Errors.ErrorCode.NotFound"/>).
    /// </exception>
    Task DeleteAsync(
        Guid householdId,
        Guid billId,
        CancellationToken cancellationToken = default);
}
