using System.Globalization;
using System.Threading.RateLimiting;
using HouseholdFinances.Api.Errors;
using HouseholdFinances.Domain.Errors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HouseholdFinances.Api.Authentication;

/// <summary>
/// Registers the rate limits that protect the anonymous authentication endpoints.
/// </summary>
/// <remarks>
/// Only endpoints that name a policy are throttled, so the health probe, the service info endpoint,
/// and the OpenAPI document are never affected. The options are resolved per request from the
/// container, so a host (or a test) can replace them without rebuilding the pipeline.
/// </remarks>
public static class RateLimitingServiceCollectionExtensions
{
    /// <summary>
    /// Partition key used when the caller's address is unknown. Every such caller shares one bucket,
    /// which is the safe choice: it throttles rather than letting the traffic through unmeasured.
    /// </summary>
    public const string UnknownClientPartition = "unknown-client";

    /// <summary>Identifier reported with a throttled request.</summary>
    public const string RateLimitIdentifier = "RateLimit";

    /// <summary>Adds the rate limiter and the authentication policies.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Configuration the limits are read from.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddHouseholdFinancesRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        AuthenticationRateLimitingOptions settings = configuration
            .GetSection(AuthenticationRateLimitingOptions.SectionName)
            .Get<AuthenticationRateLimitingOptions>()
            ?? new AuthenticationRateLimitingOptions();

        services.TryAddSingleton(settings);

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddPolicy(
                AuthRateLimitPolicies.Google,
                context => PartitionByClient(context, candidate => candidate.Google));
            options.AddPolicy(
                AuthRateLimitPolicies.Refresh,
                context => PartitionByClient(context, candidate => candidate.Refresh));

            // A throttled request is reported with the same problem+json convention as every other
            // failure, so a client has one error shape to handle.
            options.OnRejected = async (context, cancellationToken) =>
            {
                HttpContext httpContext = context.HttpContext;

                if (httpContext.Response.HasStarted)
                {
                    return;
                }

                AuthenticationRateLimitingOptions current = ResolveOptions(httpContext);
                TimeSpan retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan value)
                    ? value
                    : TimeSpan.FromSeconds(current.Refresh.WindowSeconds);

                httpContext.Response.Headers.RetryAfter =
                    Math.Ceiling(retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);

                ProblemDetails problem = ProblemResponse.Describe(
                    ErrorCode.TooManyRequests,
                    RateLimitIdentifier,
                    "Too many requests. Retry after the window elapses.");

                await ProblemResponse.WriteAsync(
                    httpContext,
                    StatusCodes.Status429TooManyRequests,
                    problem);
            };
        });

        return services;
    }

    private static RateLimitPartition<string> PartitionByClient(
        HttpContext context,
        Func<AuthenticationRateLimitingOptions, RateLimitWindow> selectWindow)
    {
        RateLimitWindow window = selectWindow(ResolveOptions(context));

        // The client is its address. A frontend instance calls the API server-to-server, so every
        // call through one instance shares that address; partitioning by a forwarded header would
        // have to trust that header, which is out of scope here.
        return RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? UnknownClientPartition,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = window.PermitLimit,
                Window = TimeSpan.FromSeconds(window.WindowSeconds),
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                AutoReplenishment = true
            });
    }

    private static AuthenticationRateLimitingOptions ResolveOptions(HttpContext context) =>
        context.RequestServices.GetRequiredService<AuthenticationRateLimitingOptions>();
}
