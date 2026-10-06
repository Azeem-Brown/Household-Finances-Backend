using HouseholdFinances.Domain.Abstractions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;

namespace HouseholdFinances.Api.Authentication;

/// <summary>
/// Registers the authentication and authorization plumbing. The provider-agnostic placeholder
/// scheme here is the single replacement point for the concrete identity provider.
/// </summary>
public static class AuthenticationServiceCollectionExtensions
{
    /// <summary>
    /// The name of the placeholder authentication scheme registered until the deferred
    /// authentication service issue (backend #11) supplies the real one.
    /// </summary>
    public const string DeferredSchemeName = "Deferred";

    /// <summary>
    /// Registers authentication and authorization services and makes an authenticated user the
    /// default requirement for every endpoint. Endpoints that are infrastructure rather than domain
    /// API (for example <c>/health</c>) opt out with <c>AllowAnonymous</c>.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The same service collection, so calls can be chained.</returns>
    public static IServiceCollection AddHouseholdFinancesAuthentication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // EXTENSION POINT (backend #11): replace this deferred, provider-agnostic scheme with the
        // concrete Google Identity token validation and API-issued JWT registration. Nothing outside
        // this method depends on the provider, so the replacement does not ripple through the app.
        services
            .AddAuthentication(DeferredSchemeName)
            .AddScheme<AuthenticationSchemeOptions, DeferredAuthenticationHandler>(
                DeferredSchemeName,
                _ => { });

        // [Authorize] is the default requirement: every endpoint requires an authenticated user
        // unless it opts out with [AllowAnonymous].
        services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
        });

        return services;
    }

    /// <summary>
    /// Registers the current-user accessor that resolves the authenticated user from the request
    /// principal.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The same service collection, so calls can be chained.</returns>
    public static IServiceCollection AddCurrentUserAccessor(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, HttpContextCurrentUserService>();

        return services;
    }
}
