using HouseholdFinances.Domain.Models;

namespace HouseholdFinances.Domain.Abstractions;

/// <summary>
/// User profile operations for the current, authenticated user. The user is always resolved from
/// <see cref="ICurrentUserService"/>; a user id is never accepted from the client.
/// </summary>
public interface IUserService
{
    /// <summary>Returns the profile of the current user.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The current user's profile.</returns>
    /// <exception cref="Errors.HouseholdFinancesException">
    /// The request is unauthenticated (<see cref="Errors.ErrorCode.Unauthorized"/>), or the current
    /// user has no stored record (<see cref="Errors.ErrorCode.NotFound"/>).
    /// </exception>
    Task<UserProfile> GetCurrentUserProfileAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the current user's display name and returns the updated profile. The name is trimmed
    /// before it is stored.
    /// </summary>
    /// <param name="name">The new display name.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The updated profile.</returns>
    /// <exception cref="Errors.HouseholdFinancesException">
    /// The name is null, empty, or whitespace (<see cref="Errors.ErrorCode.InvalidInput"/>); the
    /// request is unauthenticated (<see cref="Errors.ErrorCode.Unauthorized"/>); or the current user
    /// has no stored record (<see cref="Errors.ErrorCode.NotFound"/>).
    /// </exception>
    Task<UserProfile> UpdateCurrentUserDisplayNameAsync(
        string? name,
        CancellationToken cancellationToken = default);
}
