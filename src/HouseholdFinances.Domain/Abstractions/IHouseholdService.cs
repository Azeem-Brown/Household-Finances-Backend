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
/// authoritatively. Recurrence is not expanded in this version; that belongs to the Income and Bills
/// domains.
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
    /// Lists the current user's households, ordered by name, each with its computed totals. Empty
    /// when the user has no membership.
    /// </summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The current user's households.</returns>
    /// <exception cref="Errors.HouseholdFinancesException">
    /// The request is unauthenticated (<see cref="Errors.ErrorCode.Unauthorized"/>).
    /// </exception>
    Task<IReadOnlyList<HouseholdDetail>> ListForCurrentUserAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the household with the supplied identifier when the current user is a member of it.
    /// A household the user does not belong to is reported as not found, so the API never discloses
    /// the existence of another user's household.
    /// </summary>
    /// <param name="householdId">The household's identifier.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The household with its computed totals.</returns>
    /// <exception cref="Errors.HouseholdFinancesException">
    /// The request is unauthenticated (<see cref="Errors.ErrorCode.Unauthorized"/>) or the user is
    /// not a member of the household (<see cref="Errors.ErrorCode.NotFound"/>).
    /// </exception>
    Task<HouseholdDetail> GetByIdAsync(Guid householdId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets or replaces the household's logo. The current user must be a member. The upload is
    /// validated before anything is written, so a rejected upload leaves any existing logo in place.
    /// </summary>
    /// <param name="householdId">The household's identifier.</param>
    /// <param name="content">The image bytes.</param>
    /// <param name="contentType">The media type reported by the client, matched against the accepted image types.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <exception cref="Errors.HouseholdFinancesException">
    /// The request is unauthenticated (<see cref="Errors.ErrorCode.Unauthorized"/>); the user is not
    /// a member of the household, or no such household exists
    /// (<see cref="Errors.ErrorCode.NotFound"/>); or the upload is empty, an unsupported media type,
    /// or larger than the accepted size (<see cref="Errors.ErrorCode.InvalidInput"/>).
    /// </exception>
    Task SetLogoAsync(
        Guid householdId,
        byte[]? content,
        string? contentType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the household's logo. The current user must be a member.
    /// </summary>
    /// <param name="householdId">The household's identifier.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The logo, or <c>null</c> when the household has none.</returns>
    /// <exception cref="Errors.HouseholdFinancesException">
    /// The request is unauthenticated (<see cref="Errors.ErrorCode.Unauthorized"/>), or the user is
    /// not a member of the household (<see cref="Errors.ErrorCode.NotFound"/>).
    /// </exception>
    Task<HouseholdLogoContent?> GetLogoAsync(Guid householdId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the household's logo. The current user must be a member.
    /// </summary>
    /// <param name="householdId">The household's identifier.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns><c>true</c> when a logo was removed; <c>false</c> when the household had none.</returns>
    /// <exception cref="Errors.HouseholdFinancesException">
    /// The request is unauthenticated (<see cref="Errors.ErrorCode.Unauthorized"/>), or the user is
    /// not a member of the household (<see cref="Errors.ErrorCode.NotFound"/>).
    /// </exception>
    Task<bool> RemoveLogoAsync(Guid householdId, CancellationToken cancellationToken = default);
}
