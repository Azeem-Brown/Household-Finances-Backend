using HouseholdFinances.Api.Models;

namespace HouseholdFinances.Api.Authentication;

/// <summary>
/// Signs a user in with Google Identity and issues the API's own tokens, and refreshes those tokens.
/// </summary>
public interface IAuthenticationService
{
    /// <summary>
    /// Validates a Google ID token, provisions the user on first sign-in (keyed by the Google
    /// subject, never by email), and returns the API's token pair.
    /// </summary>
    /// <param name="idToken">The Google ID token from the frontend.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The API-issued token pair.</returns>
    /// <exception cref="Domain.Errors.HouseholdFinancesException">
    /// The token is blank (<see cref="Domain.Errors.ErrorCode.InvalidInput"/>) or fails validation
    /// (<see cref="Domain.Errors.ErrorCode.Unauthorized"/>).
    /// </exception>
    Task<AuthenticationTokenResponse> SignInWithGoogleAsync(
        string? idToken,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates a refresh token and issues a new token pair for the same user.
    /// </summary>
    /// <param name="refreshToken">The refresh token from a previous sign-in or refresh.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The API-issued token pair.</returns>
    /// <exception cref="Domain.Errors.HouseholdFinancesException">
    /// The token is blank (<see cref="Domain.Errors.ErrorCode.InvalidInput"/>), fails validation, or
    /// belongs to a user that no longer exists (<see cref="Domain.Errors.ErrorCode.Unauthorized"/>).
    /// </exception>
    Task<AuthenticationTokenResponse> RefreshAsync(
        string? refreshToken,
        CancellationToken cancellationToken = default);
}
