using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace HouseholdFinances.Api.Authentication;

/// <summary>
/// Placeholder authentication handler that never authenticates a request.
/// </summary>
/// <remarks>
/// It exists so the authentication/authorization pipeline is registered and can run before the
/// concrete scheme is supplied, which keeps the host starting and protected endpoints rejecting
/// unauthenticated requests with HTTP 401. It performs no credential or token handling and holds no
/// provider-specific configuration. The real scheme is registered by the deferred authentication
/// service issue (backend #11), which replaces this handler.
/// </remarks>
public sealed class DeferredAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    /// <summary>Creates the handler.</summary>
    /// <param name="options">The scheme options.</param>
    /// <param name="logger">The logger factory.</param>
    /// <param name="encoder">The URL encoder.</param>
    public DeferredAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    /// <inheritdoc />
    protected override Task<AuthenticateResult> HandleAuthenticateAsync() =>
        Task.FromResult(AuthenticateResult.NoResult());
}
