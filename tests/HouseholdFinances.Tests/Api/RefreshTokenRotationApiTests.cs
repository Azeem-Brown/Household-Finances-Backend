using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using HouseholdFinances.Api.Authentication;
using HouseholdFinances.Domain.Entities;
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
/// Exercises refresh-token rotation over HTTP: a successful refresh returns a new pair and consumes
/// the presented token, and reuse of a consumed token revokes the whole family so later refreshes
/// with any token from it fail with HTTP 401 in the existing problem-details shape.
/// </summary>
/// <remarks>
/// The host uses the EF Core in-memory provider, which is sufficient for the rotation and
/// revocation paths but does not prove the database-level concurrency guarantee. That is
/// demonstrated separately against the real database.
/// </remarks>
public class RefreshTokenRotationApiTests : IDisposable
{
    private const string Issuer = "HouseholdFinances.Tests";
    private const string Audience = "HouseholdFinances.Tests.Api";
    private const string RefreshAudience = "HouseholdFinances.Tests.Api.Refresh";
    private const string SigningKey = "integration-test-signing-key-that-is-long-enough-1234567890";

    private readonly Factory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Refresh_FirstUse_ReturnsANewPairAndInvalidatesThePresentedToken()
    {
        using var client = _factory.CreateClient();
        var signedIn = await SignInAsync(client, "rotation-sub-1");

        using var refreshResponse = await RefreshAsync(client, signedIn.RefreshToken);

        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
        var rotated = await ReadTokensAsync(refreshResponse);
        Assert.False(string.IsNullOrWhiteSpace(rotated.AccessToken));
        Assert.NotEqual(signedIn.RefreshToken, rotated.RefreshToken);

        // The presented token has been consumed, so presenting it again is a rejection.
        using var reuse = await RefreshAsync(client, signedIn.RefreshToken);
        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);
    }

    [Fact]
    public async Task Refresh_ReusingAConsumedToken_RevokesTheFamilyAndRejectsLaterTokens()
    {
        using var client = _factory.CreateClient();
        var signedIn = await SignInAsync(client, "rotation-sub-2");

        // First refresh succeeds and rotates to a new token in the same family.
        using var firstRefresh = await RefreshAsync(client, signedIn.RefreshToken);
        Assert.Equal(HttpStatusCode.OK, firstRefresh.StatusCode);
        var rotated = await ReadTokensAsync(firstRefresh);

        // Reuse of the consumed original is unauthorized and reported in the problem-details shape.
        using var reuse = await RefreshAsync(client, signedIn.RefreshToken);
        reuse.AssertUnauthorizedRefreshToken();

        // The whole family is revoked, so the token issued by the successful refresh is now also
        // rejected. No re-authentication is possible with any token from the family.
        using var afterReuse = await RefreshAsync(client, rotated.RefreshToken);
        afterReuse.AssertUnauthorizedRefreshToken();

        Assert.Equal(1, _factory.CountRevokedFamilies());
    }

    [Fact]
    public async Task Refresh_WithARefreshTokenThatWasNeverRecorded_ReturnsUnauthorized()
    {
        using var client = _factory.CreateClient();

        // A structurally valid, correctly signed refresh token whose jti was never recorded.
        var unrecorded = _factory.CreateUnrecordedRefreshToken();

        using var response = await RefreshAsync(client, unrecorded);

        response.AssertUnauthorizedRefreshToken();
    }

    [Fact]
    public async Task Refresh_WithAMalformedToken_ReturnsUnauthorized()
    {
        using var client = _factory.CreateClient();

        using var response = await RefreshAsync(client, "not-a-jwt");

        response.AssertUnauthorizedRefreshToken();
    }

    [Fact]
    public async Task Refresh_WithMissingToken_ReturnsInvalidInput()
    {
        using var client = _factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(2, document.RootElement.GetProperty("errorCode").GetInt32());
        Assert.Equal("refreshToken", document.RootElement.GetProperty("identifier").GetString());
    }

    private static async Task<TokenResponse> SignInAsync(HttpClient client, string subject)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/v1/auth/google",
            new { idToken = $"valid|{subject}|{subject}@example.com|Rotation User" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadTokensAsync(response);
    }

    private static Task<HttpResponseMessage> RefreshAsync(HttpClient client, string refreshToken) =>
        client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken });

    private static async Task<TokenResponse> ReadTokensAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<TokenResponse>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException("The response did not contain a token pair.");
    }

    private sealed record TokenResponse(
        string AccessToken,
        DateTime AccessTokenExpiresAtUtc,
        string RefreshToken,
        DateTime RefreshTokenExpiresAtUtc,
        string TokenType);

    /// <summary>
    /// Hosts the API with an isolated in-memory database and a stubbed Google token validator, so the
    /// sign-in to refresh path runs without real Google credentials.
    /// </summary>
    public sealed class Factory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = $"refresh-rotation-tests-{Guid.NewGuid()}";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");

            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = "Server=localhost;Database=household_finances_tests;",
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
                    SigningKey = SigningKey,
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
                services.AddSingleton<IGoogleTokenValidator, FakeGoogleTokenValidator>();
            });
        }

        public int CountRevokedFamilies()
        {
            using var scope = Services.CreateScope();
            return scope.ServiceProvider.GetRequiredService<HouseholdFinancesDbContext>()
                .RefreshTokenFamilies.Count(family => family.Revoked);
        }

        /// <summary>Signs a refresh token for a user that was never provisioned, so its jti is unknown.</summary>
        public string CreateUnrecordedRefreshToken()
        {
            var tokenService = Services.GetRequiredService<IApiTokenService>();
            return tokenService.CreateRefreshToken(new User
            {
                Id = Guid.NewGuid(),
                Name = "Unrecorded User",
                Email = "unrecorded@example.com",
            }).Token;
        }
    }

    /// <summary>Stubbed validator accepting only tokens the test explicitly marks valid.</summary>
    public sealed class FakeGoogleTokenValidator : IGoogleTokenValidator
    {
        public Task<GoogleTokenPayload> ValidateAsync(
            string idToken,
            CancellationToken cancellationToken = default)
        {
            if (!string.IsNullOrWhiteSpace(idToken) && idToken.StartsWith("valid|", StringComparison.Ordinal))
            {
                var parts = idToken.Split('|');
                if (parts.Length >= 4 && !string.IsNullOrWhiteSpace(parts[1]))
                {
                    return Task.FromResult(new GoogleTokenPayload(parts[1], parts[2], parts[3], EmailVerified: true));
                }
            }

            throw new InvalidGoogleTokenException("The stubbed Google token is not valid.");
        }
    }
}

/// <summary>Shared assertions for the 401 problem-details shape returned by the refresh endpoint.</summary>
internal static class UnauthorizedRefreshTokenAssertions
{
    public static void AssertUnauthorizedRefreshToken(this HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

        using var document = JsonDocument.Parse(body);
        Assert.Equal(3, document.RootElement.GetProperty("errorCode").GetInt32());
        Assert.Equal("Unauthorized", document.RootElement.GetProperty("errorName").GetString());
        Assert.Equal("refreshToken", document.RootElement.GetProperty("identifier").GetString());
    }
}
