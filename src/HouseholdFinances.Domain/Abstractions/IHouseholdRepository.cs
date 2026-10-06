using HouseholdFinances.Domain.Entities;
using HouseholdFinances.Domain.Models;

namespace HouseholdFinances.Domain.Abstractions;

/// <summary>
/// Persistence access for the Household domain. It reads and writes household records and the
/// many-to-many <see cref="UserHousehold"/> membership, and it computes a household's totals from
/// its entries.
/// </summary>
public interface IHouseholdRepository
{
    /// <summary>
    /// Finds a household only when the supplied user is a member of it, or <c>null</c> otherwise
    /// (including when no such household exists). A non-member therefore cannot distinguish "does
    /// not exist" from "not permitted", so the API never discloses another user's household.
    /// </summary>
    /// <param name="householdId">The household's identifier.</param>
    /// <param name="userId">The identifier of the user who must be a member.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The matching household, or <c>null</c>.</returns>
    Task<Household?> GetForMemberAsync(
        Guid householdId,
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the households the user belongs to, ordered by name. Empty when the user has no
    /// household membership.
    /// </summary>
    /// <param name="userId">The user's identifier.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The user's households.</returns>
    Task<IReadOnlyList<Household>> GetByMemberAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reports whether a membership already exists for the supplied user and household. Used to
    /// enforce that a duplicate membership is not created.
    /// </summary>
    /// <param name="householdId">The household's identifier.</param>
    /// <param name="userId">The user's identifier.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns><c>true</c> when the membership exists; otherwise <c>false</c>.</returns>
    Task<bool> MembershipExistsAsync(
        Guid householdId,
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Computes the household's raw totals from its entries: the sum of the member users' income
    /// entry values and the sum of the household's bill entry values. Each entry counts once;
    /// recurrence is not expanded in this version.
    /// </summary>
    /// <param name="householdId">The household's identifier.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The unrounded totals.</returns>
    Task<HouseholdTotals> GetTotalsAsync(
        Guid householdId,
        CancellationToken cancellationToken = default);

    /// <summary>Adds a new household to the change tracker.</summary>
    /// <param name="household">The household to add.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    Task AddAsync(Household household, CancellationToken cancellationToken = default);

    /// <summary>Adds a new membership to the change tracker.</summary>
    /// <param name="membership">The membership to add.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    Task AddMembershipAsync(UserHousehold membership, CancellationToken cancellationToken = default);

    /// <summary>Persists pending changes to the underlying store.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
