namespace HouseholdFinances.Api.Authentication;

/// <summary>
/// The identity-provider boundary. It validates a Google Identity ID token and returns the verified
/// claims the API provisions and signs in a user from. It is an interface so the provider can be
/// stubbed in tests and in local development without contacting Google.
/// </summary>
public interface IGoogleTokenValidator
{
    /// <summary>
    /// Validates the supplied Google ID token and returns its verified claims.
    /// </summary>
    /// <param name="idToken">The Google ID token from the frontend.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The verified token claims.</returns>
    /// <exception cref="InvalidGoogleTokenException">
    /// The token is missing, malformed, has an invalid signature, or fails the issuer, audience, or
    /// expiry checks.
    /// </exception>
    Task<GoogleTokenPayload> ValidateAsync(string idToken, CancellationToken cancellationToken = default);
}

/// <summary>
/// The verified claims of a Google ID token that the API provisions and signs in a user from.
/// </summary>
/// <param name="Subject">
/// The Google subject (<c>sub</c>). This is the stable user key; provisioning is keyed on it rather
/// than the email address.
/// </param>
/// <param name="Email">The email address from the token, if present.</param>
/// <param name="Name">The display name from the token, if present.</param>
/// <param name="EmailVerified">Whether Google reports the email address as verified.</param>
public sealed record GoogleTokenPayload(
    string Subject,
    string? Email,
    string? Name,
    bool EmailVerified);

/// <summary>
/// Raised when a Google ID token fails validation. The message never contains the token itself, so
/// it is safe to surface; callers translate it to HTTP 401 without logging the token.
/// </summary>
public sealed class InvalidGoogleTokenException : Exception
{
    /// <summary>Creates the exception with a non-sensitive reason.</summary>
    /// <param name="message">The reason validation failed. Must not contain the token.</param>
    public InvalidGoogleTokenException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with a non-sensitive reason and inner cause.</summary>
    /// <param name="message">The reason validation failed. Must not contain the token.</param>
    /// <param name="innerException">The underlying provider exception.</param>
    public InvalidGoogleTokenException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
