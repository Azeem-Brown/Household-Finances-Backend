using HouseholdFinances.Domain.Entities;
using HouseholdFinances.Domain.Models;

namespace HouseholdFinances.Domain.Abstractions;

/// <summary>
/// Household operations for the current, authenticated user. The user is always resolved from
/// <see cref="ICurrentUserService"/>; a user id is never accepted from the client, so one user can
/// never read or modify another user's household.
/// </summary>
/// <remarks>
/// <para>
/// <b>Current household resolution (first version).</b> Membership is many-to-many through
/// <c>UserHousehold</c>. A user with exactly one membership has that household as their current
/// household; <see cref="ListForCurrentUserAsync"/> returns every membership, and the client selects
/// the current one from the list. No separate server-side "current household" pointer or endpoint is
/// added in this version.
/// </para>
/// <para>
/// <b>Totals.</b> <c>Incomes</c> and <c>Payments</c> are computed on read from the household's
/// entries (the members' income entries and the household's bill entries) rather than stored
/// authoritatively. They are the sum of the discrete occurrences that fall inside the requested
/// calendar window, using the approved recurrence rule: occurrences step from an entry's start date
/// by its interval until its inclusive end date, and a one-off counts once when its start date is
/// inside the window. The window is derived from <see cref="TimeProvider.GetUtcNow"/>.
/// </para>
/// <para>
/// <b>Entry counts.</b> <c>IncomeEntryCount</c> and <c>BillEntryCount</c> are the number of stored
/// entries and are independent of the requested period: they are never expanded or filtered by a
/// calendar window, so a household with entries outside the selected period still reports non-zero
/// counts while its totals are zero.
/// </para>
/// </remarks>
public interface IHouseholdService
{
    /// <summary>
    /// Creates a household and the current user's membership in it, then returns the household with
    /// its computed totals. The name is trimmed before it is stored.
    /// </summary>
    /// <param name="name">The household's name.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The created household.</returns>
    /// <exception cref="Errors.HouseholdFinancesException">
    /// The name is null, empty, or whitespace (<see cref="Errors.ErrorCode.InvalidInput"/>); the
    /// request is unauthenticated (<see cref="Errors.ErrorCode.Unauthorized"/>); or the membership
    /// already exists (<see cref="Errors.ErrorCode.Conflict"/>).
    /// </exception>
    Task<HouseholdDetail> CreateAsync(string? name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the current user's households, ordered by name, each with its totals for the
    /// occurrences inside the calendar window for <paramref name="period"/>. Empty when the user has
    /// no membership.
    /// </summary>
    /// <param name="period">The calendar period whose window is totalled.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The current user's households.</returns>
    /// <exception cref="Errors.HouseholdFinancesException">
    /// The request is unauthenticated (<see cref="Errors.ErrorCode.Unauthorized"/>).
    /// </exception>
    Task<IReadOnlyList<HouseholdDetail>> ListForCurrentUserAsync(
        Interval period,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the household with the supplied identifier when the current user is a member of it,
    /// with its totals for the occurrences inside the calendar window for <paramref name="period"/>.
    /// A household the user does not belong to is reported as not found, so the API never discloses
    /// the existence of another user's household.
    /// </summary>
    /// <param name="householdId">The household's identifier.</param>
    /// <param name="period">The calendar period whose window is totalled.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The household with its windowed totals.</returns>
    /// <exception cref="Errors.HouseholdFinancesException">
    /// The request is unauthenticated (<see cref="Errors.ErrorCode.Unauthorized"/>) or the user is
    /// not a member of the household (<see cref="Errors.ErrorCode.NotFound"/>).
    /// </exception>
    Task<HouseholdDetail> GetByIdAsync(
        Guid householdId,
        Interval period,
        CancellationToken cancellationToken = default);
}
