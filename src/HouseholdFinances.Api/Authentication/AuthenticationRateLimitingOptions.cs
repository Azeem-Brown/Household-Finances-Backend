namespace HouseholdFinances.Api.Authentication;

/// <summary>
/// Rate limits for the anonymous authentication endpoints. The limits are deliberately part of
/// configuration: a frontend instance calls the API server-to-server, so calls from one instance
/// share its address, and the limits have to be tunable without a code change.
/// </summary>
public sealed class AuthenticationRateLimitingOptions
{
    /// <summary>The configuration section these options are bound from.</summary>
    public const string SectionName = "Authentication:RateLimiting";

    /// <summary>
    /// Limit for <c>POST /api/v1/auth/google</c>. Signing in happens once per session, so the
    /// default is deliberately small.
    /// </summary>
    public RateLimitWindow Google { get; set; } = new(PermitLimit: 20, WindowSeconds: 60);

    /// <summary>
    /// Limit for <c>POST /api/v1/auth/refresh</c>. Refreshing happens automatically whenever an
    /// access token expires, so this is higher than the sign-in limit.
    /// </summary>
    public RateLimitWindow Refresh { get; set; } = new(PermitLimit: 60, WindowSeconds: 60);
}

/// <summary>One endpoint's rate limit: how many requests are allowed in a fixed window.</summary>
/// <param name="PermitLimit">The number of requests permitted per window.</param>
/// <param name="WindowSeconds">The length of the window, in seconds.</param>
public sealed record RateLimitWindow(int PermitLimit, int WindowSeconds);
