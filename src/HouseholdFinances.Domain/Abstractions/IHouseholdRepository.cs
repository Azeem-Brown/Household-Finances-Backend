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
    /// Computes the household's raw totals for the occurrences that fall inside
    /// <paramref name="window"/>: the sum of the member users' income occurrences and the sum of
    /// the household's bill occurrences. Recurrence is expanded using the approved rule, and a
    /// one-off counts once when its start date lies inside the window.
    /// </summary>
    /// <param name="householdId">The household's identifier.</param>
    /// <param name="window">The inclusive calendar window to total.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The unrounded totals.</returns>
    Task<HouseholdTotals> GetTotalsAsync(
        Guid householdId,
        PeriodWindow window,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts the household's stored entries: the member users' income entries and the household's
    /// bill entries. The counts are deliberately window-independent, so nothing here is expanded or
    /// filtered by date; one recurring entry counts once.
    /// </summary>
    /// <param name="householdId">The household's identifier.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The entry counts, zero when the household has no entries.</returns>
    Task<HouseholdEntryCounts> GetEntryCountsAsync(
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

    /// <summary>
    /// Reads a household's logo, or <c>null</c> when the household has none. This does not check
    /// membership: the caller is expected to resolve the household for the current user first, so
    /// the membership boundary stays in one place.
    /// </summary>
    /// <param name="householdId">The household's identifier.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The household's logo, or <c>null</c>.</returns>
    Task<HouseholdLogo?> GetLogoAsync(Guid householdId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts the logo when the household has none, or replaces the existing one. Changes are
    /// tracked only; call <see cref="SaveChangesAsync"/> to persist them.
    /// </summary>
    /// <param name="logo">The logo to store.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    Task UpsertLogoAsync(HouseholdLogo logo, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks the household's logo for removal. Changes are tracked only; call
    /// <see cref="SaveChangesAsync"/> to persist them.
    /// </summary>
    /// <param name="householdId">The household's identifier.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns><c>true</c> when a logo was marked for removal; <c>false</c> when there was none.</returns>
    Task<bool> RemoveLogoAsync(Guid householdId, CancellationToken cancellationToken = default);

    /// <summary>Persists pending changes to the underlying store.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
