using Microsoft.Extensions.Hosting;

namespace HouseholdFinances.Api.Authentication;

/// <summary>
/// Development-only <see cref="IGoogleTokenValidator"/> that accepts a locally generated test token
/// so authenticated calls can be exercised without a live Google account or credentials.
/// </summary>
/// <remarks>
/// It is registered only when the host environment is Development and the developer has explicitly
/// opted in through <see cref="DevelopmentGoogleTokenBypassOptions"/>. Its constructor additionally
/// throws when constructed outside Development, or when the opt-in is not enabled, so the bypass
/// cannot be activated by the environment name alone or in a deployed environment. The bypass lives
/// in this separate class, so the production <see cref="GoogleIdentityTokenValidator"/> contains no
/// bypass branch. A token that does not carry the development prefix is delegated to the real
/// validator, so a developer with configured Google credentials can still sign in with a real token.
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
    /// <param name="bypassOptions">The development bypass opt-in; must be enabled.</param>
    /// <exception cref="InvalidOperationException">
    /// The host environment is not Development, or the development bypass opt-in is not enabled.
    /// This is the startup guard that keeps the development bypass unreachable unless a developer
    /// has deliberately opted in inside the Development environment.
    /// </exception>
    public DevelopmentGoogleTokenValidator(
        GoogleIdentityTokenValidator innerValidator,
        IHostEnvironment environment,
        DevelopmentGoogleTokenBypassOptions bypassOptions)
    {
        _innerValidator = innerValidator ?? throw new ArgumentNullException(nameof(innerValidator));
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(bypassOptions);

        if (!environment.IsDevelopment())
        {
            throw new InvalidOperationException(
                "The development Google token validator must not be registered outside the Development environment.");
        }

        if (!bypassOptions.Enabled)
        {
            throw new InvalidOperationException(
                $"The development Google token validator requires the '{DevelopmentGoogleTokenBypassOptions.SectionName}:Enabled' "
                + "setting to be true. It is off by default and must be opted in to per developer.");
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
