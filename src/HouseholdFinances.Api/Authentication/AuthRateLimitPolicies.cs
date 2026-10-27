namespace HouseholdFinances.Api.Authentication;

/// <summary>
/// Names of the rate-limiting policies applied to the authentication endpoints. They are constants
/// because MVC's <c>EnableRateLimitingAttribute</c> takes the policy name at compile time.
/// </summary>
public static class AuthRateLimitPolicies
{
    /// <summary>Policy for <c>POST /api/v1/auth/google</c>.</summary>
    public const string Google = "auth-google";

    /// <summary>Policy for <c>POST /api/v1/auth/refresh</c>.</summary>
    public const string Refresh = "auth-refresh";
}
