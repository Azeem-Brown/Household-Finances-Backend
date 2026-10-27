using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using HouseholdFinances.Api.Authentication;
using HouseholdFinances.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;

namespace HouseholdFinances.Tests.Api;

/// <summary>
/// Exercises the rate limits on the anonymous authentication endpoints over HTTP: requests above the
/// configured limit are rejected in the repository's error convention with a 429 and a
/// <c>Retry-After</c> header, requests under it are unaffected, and the infrastructure endpoints are
/// never throttled.
/// </summary>
/// <remarks>
/// The limits are supplied through the container with small values so the suite stays fast, which
/// also proves the limits are read from configuration rather than hard-coded. The identity-provider
/// boundary is stubbed, so no real Google credentials or network access are required.
/// </remarks>
public class AuthRateLimitingApiTests : IDisposable
{
    private const string Issuer = "HouseholdFinances.Tests";
    private const string Audience = "HouseholdFinances.Tests.Api";
    private const string RefreshAudience = "HouseholdFinances.Tests.Api.Refresh";
    private const string SigningKey = "integration-test-signing-key-that-is-long-enough-1234567890";
    private const int GoogleLimit = 2;
    private const int RefreshLimit = 3;

    private readonly Factory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public void Defaults_AreConservativeAndPerEndpoint()
    {
        var options = new AuthenticationRateLimitingOptions();

        Assert.Equal(20, options.Google.PermitLimit);
        Assert.Equal(60, options.Google.WindowSeconds);
        Assert.Equal(60, options.Refresh.PermitLimit);
        Assert.Equal(60, options.Refresh.WindowSeconds);
    }

    [Fact]
    public async Task SignInWithGoogle_UnderTheLimit_IsNotThrottled()
    {
        using var client = _factory.CreateClient();

        using var first = await PostSignInAsync(client, "valid|limit-sub-1|one@example.com|One");
        using var second = await PostSignInAsync(client, "not-a-valid-token");

        // The limit allows both: one succeeds, one fails on validation, and neither is a 429.
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, second.StatusCode);
    }

    [Fact]
    public async Task SignInWithGoogle_AboveTheLimit_ReturnsAThrottledConventionResponse()
    {
        using var client = _factory.CreateClient();

        for (int attempt = 0; attempt < GoogleLimit; attempt++)
        {
            using var allowed = await PostSignInAsync(
                client,
                $"valid|limit-sub-{attempt}|user{attempt}@example.com|User {attempt}");
            Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        }

        using var rejected = await PostSignInAsync(client, "valid|limit-sub-over|over@example.com|Over");

        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.Equal("application/problem+json", rejected.Content.Headers.ContentType?.MediaType);

        // The client is told when to come back.
        Assert.NotNull(rejected.Headers.RetryAfter);

        using JsonDocument body = JsonDocument.Parse(await rejected.Content.ReadAsStringAsync());
        JsonElement root = body.RootElement;

        Assert.Equal(5, root.GetProperty("errorCode").GetInt32());
        Assert.Equal(nameof(HouseholdFinances.Domain.Errors.ErrorCode.TooManyRequests), root.GetProperty("errorName").GetString());
        Assert.Equal(RateLimitingServiceCollectionExtensions.RateLimitIdentifier, root.GetProperty("identifier").GetString());
        Assert.Equal("Too Many Requests", root.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Refresh_AboveTheLimit_ReturnsAThrottledConventionResponse()
    {
        using var client = _factory.CreateClient();

        for (int attempt = 0; attempt < RefreshLimit; attempt++)
        {
            using var allowed = await PostRefreshAsync(client, "not-a-refresh-token");
            Assert.NotEqual(HttpStatusCode.TooManyRequests, allowed.StatusCode);
        }

        using var rejected = await PostRefreshAsync(client, "not-a-refresh-token");

        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.Equal("application/problem+json", rejected.Content.Headers.ContentType?.MediaType);

        using JsonDocument body = JsonDocument.Parse(await rejected.Content.ReadAsStringAsync());
        Assert.Equal(5, body.RootElement.GetProperty("errorCode").GetInt32());
    }

    [Fact]
    public async Task InfrastructureEndpoints_AreNeverThrottledByTheAuthPolicies()
    {
        using var client = _factory.CreateClient();

        // More requests than either auth limit allows.
        for (int attempt = 0; attempt < GoogleLimit * 5; attempt++)
        {
            using var health = await client.GetAsync("/health");
            Assert.Equal(HttpStatusCode.OK, health.StatusCode);

            using var info = await client.GetAsync("/");
            Assert.Equal(HttpStatusCode.OK, info.StatusCode);
        }
    }

    private static Task<HttpResponseMessage> PostSignInAsync(HttpClient client, string idToken) =>
        client.PostAsJsonAsync("/api/v1/auth/google", new { idToken });

    private static Task<HttpResponseMessage> PostRefreshAsync(HttpClient client, string refreshToken) =>
        client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken });

    /// <summary>
    /// Hosts the API with an isolated in-memory store, stubbed Google validation, and small rate
    /// limits supplied through the container.
    /// </summary>
    private sealed class Factory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = $"auth-rate-limit-tests-{Guid.NewGuid()}";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");

            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = "Server=localhost;Database=household_finances_tests;",
                    ["Authentication:Jwt:SigningKey"] = SigningKey,
                    ["Authentication:Jwt:Issuer"] = Issuer,
                    ["Authentication:Jwt:Audience"] = Audience,
                    ["Authentication:Jwt:RefreshAudience"] = RefreshAudience
                });
            });

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<JwtOptions>();
                services.AddSingleton(new JwtOptions
                {
                    Issuer = Issuer,
                    Audience = Audience,
                    RefreshAudience = RefreshAudience,
                    SigningKey = SigningKey
                });

                services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
                {
                    options.TokenValidationParameters.ValidIssuer = Issuer;
                    options.TokenValidationParameters.ValidAudience = Audience;
                    options.TokenValidationParameters.IssuerSigningKey =
                        new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey));
                });

                services.RemoveAll<DbContextOptions<HouseholdFinancesDbContext>>();
                services.AddSingleton(
                    new DbContextOptionsBuilder<HouseholdFinancesDbContext>()
                        .UseInMemoryDatabase(_databaseName)
                        .Options);

                services.RemoveAll<IGoogleTokenValidator>();
                services.AddSingleton<IGoogleTokenValidator, GoogleAuthenticationApiTests.FakeGoogleTokenValidator>();

                // Small limits so a test can reach the boundary in a couple of requests.
                services.RemoveAll<AuthenticationRateLimitingOptions>();
                services.AddSingleton(new AuthenticationRateLimitingOptions
                {
                    Google = new RateLimitWindow(GoogleLimit, 60),
                    Refresh = new RateLimitWindow(RefreshLimit, 60)
                });
            });
        }
    }
}
