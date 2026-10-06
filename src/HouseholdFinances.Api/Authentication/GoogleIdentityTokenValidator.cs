using Google.Apis.Auth;

namespace HouseholdFinances.Api.Authentication;

/// <summary>
/// Validates Google Identity ID tokens against Google's published signing certificates.
/// </summary>
/// <remarks>
/// Validation is delegated to <see cref="GoogleJsonWebSignature"/>, which verifies the token
/// signature against Google's published keys, requires a Google issuer, enforces the configured
/// audience (the OAuth client id), and checks the expiry. This is deliberately not hand-rolled:
/// accepting an unverified or <c>alg: none</c> token would be an authentication bypass. The raw
/// token is never logged, so a validation failure cannot leak credentials into logs.
/// </remarks>
public sealed class GoogleIdentityTokenValidator : IGoogleTokenValidator
{
    private readonly GoogleIdentityOptions _options;

    /// <summary>Creates the validator over the supplied Google Identity options.</summary>
    /// <param name="options">The Google client id and clock tolerance.</param>
    public GoogleIdentityTokenValidator(GoogleIdentityOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public async Task<GoogleTokenPayload> ValidateAsync(
        string idToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(idToken))
        {
            throw new InvalidGoogleTokenException("No Google ID token was supplied.");
        }

        var clientId = _options.ClientId;
        if (string.IsNullOrWhiteSpace(clientId))
        {
            // Without a configured audience the token cannot be validated safely, so reject it
            // rather than skipping the audience check.
            throw new InvalidGoogleTokenException("The Google client id is not configured.");
        }

        var settings = new GoogleJsonWebSignature.ValidationSettings
        {
            Audience = new[] { clientId },
            IssuedAtClockTolerance = TimeSpan.FromMinutes(_options.ClockSkewMinutes),
            ExpirationTimeClockTolerance = TimeSpan.FromMinutes(_options.ClockSkewMinutes),
        };

        try
        {
            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, settings);

            if (string.IsNullOrWhiteSpace(payload.Subject))
            {
                throw new InvalidGoogleTokenException("The Google token has no subject claim.");
            }

            return new GoogleTokenPayload(
                payload.Subject,
                payload.Email,
                payload.Name,
                payload.EmailVerified);
        }
        catch (InvalidJwtException exception)
        {
            // The provider message describes why validation failed but does not include the token.
            throw new InvalidGoogleTokenException("The Google ID token failed validation.", exception);
        }
    }
}
