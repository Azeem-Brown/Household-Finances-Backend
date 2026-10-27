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
    /// <returns>The signed refresh token, its expiry, and its <c>jti</c>.</returns>
    ApiToken CreateRefreshToken(User user);

    /// <summary>
    /// Validates a refresh token and returns the user identifier and unique token identifier it
    /// carries. Access tokens are rejected because they carry a different audience and token-use
    /// claim.
    /// </summary>
    /// <param name="refreshToken">The refresh token to validate.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The identifier of the user the refresh token was issued to, and its <c>jti</c>.</returns>
    /// <exception cref="InvalidRefreshTokenException">
    /// The token is missing, malformed, expired, or not a refresh token.
    /// </exception>
    Task<ValidatedRefreshToken> ValidateRefreshTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken = default);
}

/// <summary>A signed API token and the UTC instant at which it expires.</summary>
/// <param name="Token">The serialized JWT.</param>
/// <param name="ExpiresAtUtc">The token's expiry in UTC.</param>
/// <param name="Jti">
/// The token's unique identifier. For a refresh token this is the value recorded server-side and
/// matched on when the token is later presented.
/// </param>
public sealed record ApiToken(string Token, DateTime ExpiresAtUtc, string Jti);

/// <summary>
/// The non-secret facts a validated refresh token carries: the user it was issued to and its unique
/// identifier. The token value itself is not retained.
/// </summary>
/// <param name="UserId">The identifier of the user the refresh token was issued to.</param>
/// <param name="Jti">The token's unique identifier, matched against the recorded row.</param>
public sealed record ValidatedRefreshToken(Guid UserId, string Jti);

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
