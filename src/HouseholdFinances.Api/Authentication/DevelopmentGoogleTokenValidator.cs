using Microsoft.Extensions.Hosting;

namespace HouseholdFinances.Api.Authentication;

/// <summary>
/// Development-only <see cref="IGoogleTokenValidator"/> that accepts a locally generated test token
/// so authenticated calls can be exercised without a live Google account or credentials.
/// </summary>
/// <remarks>
/// It is registered only when the host environment is Development, and its constructor additionally
/// throws when constructed outside Development, so the bypass cannot be activated in a deployed
/// environment. The bypass lives in this separate class, so the production
/// <see cref="GoogleIdentityTokenValidator"/> contains no bypass branch. A token that does not carry
/// the development prefix is delegated to the real validator, so a developer with configured Google
/// credentials can still sign in with a real token.
/// </remarks>
public sealed class DevelopmentGoogleTokenValidator : IGoogleTokenValidator
{
    /// <summary>
    /// Prefix that marks a locally generated development token. The remainder of the token is the
    /// Google subject to authenticate as, for example <c>dev:local-user-1</c>.
    /// </summary>
    public const string TokenPrefix = "dev:";

    private readonly GoogleIdentityTokenValidator _innerValidator;

    /// <summary>Creates the validator.</summary>
    /// <param name="innerValidator">The real validator, used for tokens without the development prefix.</param>
    /// <param name="environment">The host environment; must be Development.</param>
    /// <exception cref="InvalidOperationException">
    /// The host environment is not Development. This is the startup guard that keeps the development
    /// bypass unreachable in a deployed environment.
    /// </exception>
    public DevelopmentGoogleTokenValidator(
        GoogleIdentityTokenValidator innerValidator,
        IHostEnvironment environment)
    {
        _innerValidator = innerValidator ?? throw new ArgumentNullException(nameof(innerValidator));
        ArgumentNullException.ThrowIfNull(environment);

        if (!environment.IsDevelopment())
        {
            throw new InvalidOperationException(
                "The development Google token validator must not be registered outside the Development environment.");
        }
    }

    /// <inheritdoc />
    public Task<GoogleTokenPayload> ValidateAsync(
        string idToken,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(idToken)
            && idToken.StartsWith(TokenPrefix, StringComparison.Ordinal))
        {
            var subject = idToken[TokenPrefix.Length..].Trim();
            if (subject.Length == 0)
            {
                throw new InvalidGoogleTokenException("The development token has no subject.");
            }

            var payload = new GoogleTokenPayload(
                subject,
                $"{subject}@dev.local",
                $"Development {subject}",
                EmailVerified: true);

            return Task.FromResult(payload);
        }

        return _innerValidator.ValidateAsync(idToken, cancellationToken);
    }
}
