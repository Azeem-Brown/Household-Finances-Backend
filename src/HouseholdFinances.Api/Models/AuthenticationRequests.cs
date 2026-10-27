namespace HouseholdFinances.Api.Models;

/// <summary>
/// Request body for signing in with Google Identity. The frontend obtains the Google ID token and
/// posts it here; the API validates it and returns its own token pair in
/// <see cref="AuthenticationTokenResponse"/>.
/// </summary>
/// <param name="IdToken">The Google ID token issued to the frontend.</param>
public sealed record GoogleSignInRequest(string? IdToken);

/// <summary>
/// Request body for exchanging a refresh token for a new token pair.
/// </summary>
/// <param name="RefreshToken">The refresh token returned by a previous sign-in or refresh.</param>
public sealed record RefreshTokenRequest(string? RefreshToken);
