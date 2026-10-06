using System.Net;
using System.Net.Http.Headers;
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
/// Exercises the Google Identity sign-in flow over HTTP: validation and rejection of the Google
/// token, provisioning keyed by subject, API JWT issuance, acceptance of that JWT on a protected
/// endpoint, refresh, and the 401 path. The identity-provider boundary is stubbed, so no real Google
/// credentials or network access are required.
/// </summary>
public class GoogleAuthenticationApiTests : IDisposable
{
    private const string Issuer = "HouseholdFinances.Tests";
    private const string Audience = "HouseholdFinances.Tests.Api";
    private const string RefreshAudience = "HouseholdFinances.Tests.Api.Refresh";
    private const string SigningKey = "integration-test-signing-key-that-is-long-enough-1234567890";

    private readonly Factory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task SignInWithGoogle_ValidToken_ProvisionsUserAndReturnsApiTokens()
    {
        using var client = _factory.CreateClient();

        using var response = await PostSignInAsync(client, "valid|google-sub-1|first@example.com|First User");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var tokens = await ReadTokensAsync(response);

        Assert.False(string.IsNullOrWhiteSpace(tokens.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(tokens.RefreshToken));
        Assert.Equal("Bearer", tokens.TokenType);
        Assert.True(tokens.AccessTokenExpiresAtUtc > DateTime.UtcNow);
        Assert.True(tokens.RefreshTokenExpiresAtUtc > tokens.AccessTokenExpiresAtUtc);

        var user = _factory.FindUser("google-sub-1");
        Assert.NotNull(user);
        Assert.Equal("first@example.com", user!.Email);
        Assert.Equal("First User", user.Name);
        // No local password is ever stored for a Google-provisioned user.
        Assert.Null(user.Password);
    }

    [Fact]
    public async Task SignInWithGoogle_ReturningSubject_ReusesExistingUser()
    {
        using var client = _factory.CreateClient();

        using var first = await PostSignInAsync(client, "valid|google-sub-2|a@example.com|Name A");
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var firstUserId = _factory.FindUser("google-sub-2")!.Id;

        // Same subject but a different email and name: the subject is the key, so the user is reused
        // and the stored record is not overwritten.
        using var second = await PostSignInAsync(client, "valid|google-sub-2|b@example.com|Name B");
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var reused = _factory.FindUser("google-sub-2")!;

        Assert.Equal(firstUserId, reused.Id);
        Assert.Equal("a@example.com", reused.Email);
        Assert.Equal(1, _factory.CountUsers());
    }

    [Theory]
    [InlineData("expired|google-sub-3")]
    [InlineData("wrong-audience|google-sub-3")]
    [InlineData("tampered|google-sub-3")]
    [InlineData("not-a-google-token")]
    public async Task SignInWithGoogle_InvalidToken_ReturnsUnauthorizedAndProvisionsNothing(string idToken)
    {
        using var client = _factory.CreateClient();

        using var response = await PostSignInAsync(client, idToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        Assert.Equal(3, document.RootElement.GetProperty("errorCode").GetInt32());
        Assert.Equal("Unauthorized", document.RootElement.GetProperty("errorName").GetString());

        // The rejected token must not be echoed back, and no user or token is produced.
        Assert.DoesNotContain(idToken, body, StringComparison.Ordinal);
        Assert.Equal(0, _factory.CountUsers());
    }

    [Fact]
    public async Task SignInWithGoogle_MissingToken_ReturnsInvalidInput()
    {
        using var client = _factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/v1/auth/google", new { idToken = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(2, document.RootElement.GetProperty("errorCode").GetInt32());
        Assert.Equal("idToken", document.RootElement.GetProperty("identifier").GetString());
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_ReturnsUnauthorized()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/api/v1/users/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ApiIssuedJwt_IsAcceptedByProtectedEndpoint()
    {
        using var client = _factory.CreateClient();
        using var signIn = await PostSignInAsync(client, "valid|google-sub-4|user4@example.com|User Four");
        var tokens = await ReadTokensAsync(signIn);
        var expectedUserId = _factory.FindUser("google-sub-4")!.Id;

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        using var response = await client.GetAsync("/api/v1/users/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        // The API JWT's subject resolves through ICurrentUserService to the provisioned user.
        Assert.Equal(expectedUserId, document.RootElement.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task ProtectedEndpoint_WithTamperedApiJwt_ReturnsUnauthorized()
    {
        using var client = _factory.CreateClient();
        using var signIn = await PostSignInAsync(client, "valid|google-sub-5|user5@example.com|User Five");
        var tokens = await ReadTokensAsync(signIn);

        var last = tokens.AccessToken[^1];
        var tampered = tokens.AccessToken[..^1] + (last == 'a' ? 'b' : 'a');
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tampered);

        using var response = await client.GetAsync("/api/v1/users/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_ReturnsNewAccessTokenThatIsAccepted()
    {
        using var client = _factory.CreateClient();
        using var signIn = await PostSignInAsync(client, "valid|google-sub-6|user6@example.com|User Six");
        var tokens = await ReadTokensAsync(signIn);

        using var refreshResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new { refreshToken = tokens.RefreshToken });
        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
        var refreshed = await ReadTokensAsync(refreshResponse);
        Assert.False(string.IsNullOrWhiteSpace(refreshed.AccessToken));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", refreshed.AccessToken);
        using var profile = await client.GetAsync("/api/v1/users/me");

        Assert.Equal(HttpStatusCode.OK, profile.StatusCode);
    }

    [Fact]
    public async Task Refresh_WithAccessToken_ReturnsUnauthorized()
    {
        using var client = _factory.CreateClient();
        using var signIn = await PostSignInAsync(client, "valid|google-sub-7|user7@example.com|User Seven");
        var tokens = await ReadTokensAsync(signIn);

        // An access token must not be usable as a refresh token.
        using var response = await client.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new { refreshToken = tokens.AccessToken });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithRefreshToken_ReturnsUnauthorized()
    {
        using var client = _factory.CreateClient();
        using var signIn = await PostSignInAsync(client, "valid|google-sub-8|user8@example.com|User Eight");
        var tokens = await ReadTokensAsync(signIn);

        // A refresh token must not be usable as an access token.
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.RefreshToken);
        using var response = await client.GetAsync("/api/v1/users/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ExpiredApiAccessToken_IsRejectedByProtectedEndpoint()
    {
        using var factory = new Factory(accessTokenLifetimeMinutes: -5);
        using var client = factory.CreateClient();
        using var signIn = await PostSignInAsync(client, "valid|google-sub-9|user9@example.com|User Nine");
        var tokens = await ReadTokensAsync(signIn);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        using var response = await client.GetAsync("/api/v1/users/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static Task<HttpResponseMessage> PostSignInAsync(HttpClient client, string idToken) =>
        client.PostAsJsonAsync("/api/v1/auth/google", new { idToken });

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
    /// full sign-in to protected-call path runs without real Google credentials.
    /// </summary>
    public sealed class Factory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = $"google-auth-tests-{Guid.NewGuid()}";
        private readonly int _accessTokenLifetimeMinutes;

        public Factory(int accessTokenLifetimeMinutes = 30) =>
            _accessTokenLifetimeMinutes = accessTokenLifetimeMinutes;

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
                // Program.cs reads the JWT settings from configuration before the factory's
                // ConfigureAppConfiguration sources are applied, so override the bound options
                // directly and keep the bearer validation parameters in step. The issuing service
                // and the validator then agree on the issuer, audience, and signing key, and the
                // per-test access-token lifetime is honored.
                services.RemoveAll<JwtOptions>();
                services.AddSingleton(new JwtOptions
                {
                    Issuer = Issuer,
                    Audience = Audience,
                    RefreshAudience = RefreshAudience,
                    SigningKey = SigningKey,
                    AccessTokenLifetimeMinutes = _accessTokenLifetimeMinutes,
                });

                services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
                {
                    options.TokenValidationParameters.ValidIssuer = Issuer;
                    options.TokenValidationParameters.ValidAudience = Audience;
                    options.TokenValidationParameters.IssuerSigningKey =
                        new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey));
                });

                // Replace the MySQL options with an isolated in-memory store.
                services.RemoveAll<DbContextOptions<HouseholdFinancesDbContext>>();
                services.AddSingleton(
                    new DbContextOptionsBuilder<HouseholdFinancesDbContext>()
                        .UseInMemoryDatabase(_databaseName)
                        .Options);

                // Stub the identity-provider boundary so no network or Google credentials are needed.
                services.RemoveAll<IGoogleTokenValidator>();
                services.AddSingleton<IGoogleTokenValidator, FakeGoogleTokenValidator>();
            });
        }

        public int CountUsers()
        {
            using var scope = Services.CreateScope();
            return scope.ServiceProvider.GetRequiredService<HouseholdFinancesDbContext>().Users.Count();
        }

        public User? FindUser(string googleSubject)
        {
            using var scope = Services.CreateScope();
            return scope.ServiceProvider.GetRequiredService<HouseholdFinancesDbContext>()
                .Users.AsNoTracking()
                .FirstOrDefault(user => user.GoogleSubject == googleSubject);
        }
    }

    /// <summary>
    /// Stubbed validator. It accepts only tokens the test explicitly marks valid and rejects
    /// expired, wrong-audience, and tampered tokens, so the rejection paths can be exercised without
    /// contacting Google.
    /// </summary>
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
