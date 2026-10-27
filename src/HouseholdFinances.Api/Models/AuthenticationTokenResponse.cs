namespace HouseholdFinances.Api.Models;

/// <summary>
/// The token pair returned after a successful sign-in or refresh. The frontend sends the access
/// token as <c>Authorization: Bearer &lt;accessToken&gt;</c> on protected calls and exchanges the
/// refresh token at <c>POST /api/v1/auth/refresh</c> when the access token expires.
/// </summary>
/// <param name="AccessToken">The API-issued JWT to send on protected requests.</param>
/// <param name="AccessTokenExpiresAtUtc">The access token's expiry in UTC.</param>
/// <param name="RefreshToken">The token to exchange for a new access token.</param>
/// <param name="RefreshTokenExpiresAtUtc">The refresh token's expiry in UTC.</param>
/// <param name="TokenType">The authorization scheme; always <c>Bearer</c>.</param>
public sealed record AuthenticationTokenResponse(
    string AccessToken,
    DateTime AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTime RefreshTokenExpiresAtUtc,
    string TokenType);
