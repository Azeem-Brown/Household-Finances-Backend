using HouseholdFinances.Domain.Entities;

namespace HouseholdFinances.Api.Authentication;

/// <summary>
/// Issues and validates the API's own JWTs. Access tokens authorize requests; refresh tokens are
/// exchanged at the refresh endpoint for a new access token.
/// </summary>
public interface IApiTokenService
{
    /// <summary>Creates a short-lived access token for the supplied user.</summary>
    /// <param name="user">The authenticated user.</param>
    /// <returns>The signed access token and its expiry.</returns>
    ApiToken CreateAccessToken(User user);

    /// <summary>Creates a longer-lived refresh token for the supplied user.</summary>
    /// <param name="user">The authenticated user.</param>
    /// <returns>The signed refresh token and its expiry.</returns>
    ApiToken CreateRefreshToken(User user);

    /// <summary>
    /// Validates a refresh token and returns the user identifier it carries. Access tokens are
    /// rejected because they carry a different audience and token-use claim.
    /// </summary>
    /// <param name="refreshToken">The refresh token to validate.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The identifier of the user the refresh token was issued to.</returns>
    /// <exception cref="InvalidRefreshTokenException">
    /// The token is missing, malformed, expired, or not a refresh token.
    /// </exception>
    Task<Guid> ValidateRefreshTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken = default);
}

/// <summary>A signed API token and the UTC instant at which it expires.</summary>
/// <param name="Token">The serialized JWT.</param>
/// <param name="ExpiresAtUtc">The token's expiry in UTC.</param>
public sealed record ApiToken(string Token, DateTime ExpiresAtUtc);

/// <summary>
/// Raised when a refresh token fails validation. The message never contains the token itself, so it
/// is safe to surface; callers translate it to HTTP 401 without logging the token.
/// </summary>
public sealed class InvalidRefreshTokenException : Exception
{
    /// <summary>Creates the exception with a non-sensitive reason.</summary>
    /// <param name="message">The reason validation failed. Must not contain the token.</param>
    public InvalidRefreshTokenException(string message)
        : base(message)
    {
    }
}
