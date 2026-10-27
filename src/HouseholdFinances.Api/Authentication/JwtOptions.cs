namespace HouseholdFinances.Api.Authentication;

/// <summary>
/// Configuration for the API-issued JWT. The signing key is never committed: it is supplied through
/// user secrets for local development and an environment variable (or secret manager) in deployed
/// environments. This API signs and validates its own tokens; Google tokens are never accepted by
/// this scheme.
/// </summary>
public sealed class JwtOptions
{
    /// <summary>The configuration section that binds these options.</summary>
    public const string SectionName = "Authentication:Jwt";

    /// <summary>
    /// The token issuer. Access and refresh tokens both carry it, and incoming tokens must match it.
    /// </summary>
    public string Issuer { get; set; } = "HouseholdFinances";

    /// <summary>
    /// The audience carried by API access tokens. The bearer scheme accepts only this audience, so a
    /// refresh token (which carries <see cref="RefreshAudience"/>) cannot be used as an access token.
    /// </summary>
    public string Audience { get; set; } = "HouseholdFinances.Api";

    /// <summary>
    /// The audience carried by refresh tokens. It differs from <see cref="Audience"/> so the two
    /// token kinds cannot be confused for one another.
    /// </summary>
    public string RefreshAudience { get; set; } = "HouseholdFinances.Api.Refresh";

    /// <summary>
    /// The symmetric key used to sign and validate API tokens. Required outside Development; in
    /// Development an ephemeral key is generated when it is absent so the host starts without
    /// committed credentials. Never logged.
    /// </summary>
    public string? SigningKey { get; set; }

    /// <summary>The lifetime, in minutes, of an issued access token.</summary>
    public int AccessTokenLifetimeMinutes { get; set; } = 30;

    /// <summary>
    /// The lifetime, in days, of an issued refresh token. Refresh tokens are recorded server-side and
    /// rotated on every successful refresh: the presented token is consumed and a fresh pair is
    /// issued in the same family. Reuse of a consumed token revokes the family, so a leaked token
    /// cannot be used silently.
    /// </summary>
    public int RefreshTokenLifetimeDays { get; set; } = 7;
}
