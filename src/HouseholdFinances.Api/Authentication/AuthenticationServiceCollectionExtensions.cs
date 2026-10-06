using System.Security.Cryptography;
using System.Text;
using HouseholdFinances.Domain.Abstractions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

namespace HouseholdFinances.Api.Authentication;

/// <summary>
/// Registers the authentication and authorization plumbing: Google Identity token validation, the
/// API's own JWT issuance and validation, and the current-user accessor.
/// </summary>
public static class AuthenticationServiceCollectionExtensions
{
    /// <summary>
    /// Registers authentication and authorization services and makes an authenticated user the
    /// default requirement for every endpoint. Endpoints that are infrastructure rather than domain
    /// API (for example <c>/health</c>) opt out with <c>AllowAnonymous</c>.
    /// </summary>
    /// <remarks>
    /// The API issues and validates its own JWTs with a symmetric key from configuration; a Google
    /// token is only ever accepted by the Google-identity sign-in endpoint, never by the bearer
    /// scheme. Required secrets are enforced outside Development so a deployed host cannot start
    /// without them.
    /// </remarks>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configuration">The configuration that supplies the JWT and Google settings.</param>
    /// <param name="environment">The host environment, used to scope development-only behavior.</param>
    /// <returns>The same service collection, so calls can be chained.</returns>
    public static IServiceCollection AddHouseholdFinancesAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        var isDevelopment = environment.IsDevelopment();

        var jwtOptions = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
            ?? new JwtOptions();
        if (string.IsNullOrWhiteSpace(jwtOptions.SigningKey))
        {
            if (!isDevelopment)
            {
                throw new InvalidOperationException(
                    $"The JWT signing key '{JwtOptions.SectionName}:SigningKey' is not configured. "
                    + "Provide it with 'dotnet user-secrets set \"Authentication:Jwt:SigningKey\" \"<value>\"' "
                    + "for local development, or the 'Authentication__Jwt__SigningKey' environment variable in a deployed environment.");
            }

            // Development-only fallback so the host starts without committed credentials. The key is
            // ephemeral, so tokens stop validating when the process restarts.
            jwtOptions.SigningKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        }

        if (Encoding.UTF8.GetByteCount(jwtOptions.SigningKey) < 32)
        {
            throw new InvalidOperationException(
                $"{JwtOptions.SectionName}:SigningKey must be at least 256 bits (32 bytes) long.");
        }

        var googleOptions = configuration.GetSection(GoogleIdentityOptions.SectionName)
            .Get<GoogleIdentityOptions>() ?? new GoogleIdentityOptions();
        if (!isDevelopment && string.IsNullOrWhiteSpace(googleOptions.ClientId))
        {
            throw new InvalidOperationException(
                $"The Google client id '{GoogleIdentityOptions.SectionName}:ClientId' is not configured. "
                + "Provide it with 'dotnet user-secrets set \"Authentication:Google:ClientId\" \"<value>\"' "
                + "for local development, or the 'Authentication__Google__ClientId' environment variable in a deployed environment.");
        }

        services.AddSingleton(jwtOptions);
        services.AddSingleton(googleOptions);

        services.AddSingleton<IApiTokenService, ApiTokenService>();
        services.AddScoped<IAuthenticationService, AuthenticationService>();
        services.AddSingleton<GoogleIdentityTokenValidator>();

        if (isDevelopment)
        {
            // The development bypass is a separate class whose constructor refuses to be constructed
            // outside Development, so it can never be active in a deployed environment.
            services.AddSingleton<IGoogleTokenValidator, DevelopmentGoogleTokenValidator>();
        }
        else
        {
            services.AddSingleton<IGoogleTokenValidator, GoogleIdentityTokenValidator>();
        }

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey));

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                // The API issues ClaimTypes.NameIdentifier explicitly, so inbound claims are not
                // remapped; ICurrentUserService reads exactly that claim.
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidateAudience = true,
                    // Only access tokens carry this audience, so a refresh token is structurally
                    // rejected here rather than being treated as an access token.
                    ValidAudience = jwtOptions.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = signingKey,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(1),
                    ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 },
                };
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = context =>
                    {
                        // Defense in depth: the principal must be an access token. The audience check
                        // already excludes refresh tokens; this excludes any future token kind that
                        // shares the access audience.
                        var tokenUse = context.Principal?
                            .FindFirst(ApiTokenService.TokenUseClaimType)?.Value;
                        if (!string.Equals(tokenUse, ApiTokenService.AccessTokenUse, StringComparison.Ordinal))
                        {
                            context.Fail("The token is not an access token.");
                        }

                        return Task.CompletedTask;
                    },
                };
            });

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
