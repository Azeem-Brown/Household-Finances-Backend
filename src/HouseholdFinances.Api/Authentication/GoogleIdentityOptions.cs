namespace HouseholdFinances.Api.Authentication;

/// <summary>
/// Configuration for the Google Identity provider. The client id is the OAuth client id the frontend
/// obtains the Google ID token with, and is the audience the incoming token must be addressed to.
/// The client secret is not needed to validate an ID token, but is retained for any future
/// server-side code exchange. Neither value is committed.
/// </summary>
public sealed class GoogleIdentityOptions
{
    /// <summary>The configuration section that binds these options.</summary>
    public const string SectionName = "Authentication:Google";

    /// <summary>
    /// The expected audience of an incoming Google ID token (the OAuth client id). Required outside
    /// Development; the real validator rejects every token when it is not configured.
    /// </summary>
    public string? ClientId { get; set; }

    /// <summary>Reserved for a future authorization-code exchange; not used to validate ID tokens.</summary>
    public string? ClientSecret { get; set; }

    /// <summary>Clock tolerance, in minutes, applied when validating the Google token's time claims.</summary>
    public int ClockSkewMinutes { get; set; } = 5;
}
