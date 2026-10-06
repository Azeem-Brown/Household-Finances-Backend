using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using HouseholdFinances.Domain.Entities;
using HouseholdFinances.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HouseholdFinances.Tests.Api;

/// <summary>
/// Exercises the User profile endpoints over HTTP: the authorization requirement, the successful
/// profile read and display-name update, the error convention for invalid input and unknown users,
/// and that no password is ever returned.
/// </summary>
public class UsersApiTests : IDisposable
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid HouseholdId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly Factory _factory = new();

    public UsersApiTests()
    {
        // Each test gets an isolated in-memory database, so one test cannot affect another.
        _factory.Seed();
    }

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task GetCurrentUserProfile_WithoutAuthentication_ReturnsUnauthorized()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/api/v1/users/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UpdateCurrentUserDisplayName_WithoutAuthentication_ReturnsUnauthorized()
    {
        using var client = _factory.CreateClient();

        using var response = await client.PatchAsJsonAsync("/api/v1/users/me", new { name = "New Name" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetCurrentUserProfile_ReturnsNameEmailAndHouseholdAssociationWithoutPassword()
    {
        using var client = CreateAuthenticatedClient(UserId);

        using var response = await client.GetAsync("/api/v1/users/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal(UserId, root.GetProperty("id").GetGuid());
        Assert.Equal("Alex Doe", root.GetProperty("name").GetString());
        Assert.Equal("alex@example.com", root.GetProperty("email").GetString());

        var household = Assert.Single(root.GetProperty("households").EnumerateArray());
        Assert.Equal(HouseholdId, household.GetProperty("id").GetGuid());
        Assert.Equal("Brown Household", household.GetProperty("name").GetString());

        Assert.False(root.TryGetProperty("password", out _));
        Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("unused-hash", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateCurrentUserDisplayName_PersistsAndIsReturnedByASubsequentGet()
    {
        using var client = CreateAuthenticatedClient(UserId);

        using var update = await client.PatchAsJsonAsync("/api/v1/users/me", new { name = "  Alexandria  " });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        using (var updateDocument = JsonDocument.Parse(await update.Content.ReadAsStringAsync()))
        {
            Assert.Equal("Alexandria", updateDocument.RootElement.GetProperty("name").GetString());
        }

        using var get = await client.GetAsync("/api/v1/users/me");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        using var getDocument = JsonDocument.Parse(await get.Content.ReadAsStringAsync());
        Assert.Equal("Alexandria", getDocument.RootElement.GetProperty("name").GetString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UpdateCurrentUserDisplayName_WithABlankName_ReturnsInvalidInputProblem(string name)
    {
        using var client = CreateAuthenticatedClient(UserId);

        using var response = await client.PatchAsJsonAsync("/api/v1/users/me", new { name });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        Assert.Equal(2, root.GetProperty("errorCode").GetInt32());
        Assert.Equal("InvalidInput", root.GetProperty("errorName").GetString());
        Assert.Equal("Name", root.GetProperty("identifier").GetString());
        Assert.Equal("InvalidInput Name", root.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task GetCurrentUserProfile_ForAnUnknownUser_ReturnsNotFoundProblem()
    {
        using var client = CreateAuthenticatedClient(Guid.NewGuid());

        using var response = await client.GetAsync("/api/v1/users/me");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        Assert.Equal(1, root.GetProperty("errorCode").GetInt32());
        Assert.Equal("NotFound", root.GetProperty("errorName").GetString());
    }

    private HttpClient CreateAuthenticatedClient(Guid userId)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.HeaderName, userId.ToString());
        return client;
    }

    /// <summary>
    /// Hosts the API with an isolated in-memory database and a test authentication scheme that
    /// authenticates the user id carried by a request header.
    /// </summary>
    public sealed class Factory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = $"users-tests-{Guid.NewGuid()}";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");

            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = "Server=localhost;Database=household_finances_tests;"
                });
            });

            builder.ConfigureServices(services =>
            {
                // Replace the MySQL options registered by the host with an isolated in-memory store.
                // Overriding DbContextOptions bypasses the provider configured in Program.cs.
                services.RemoveAll<DbContextOptions<HouseholdFinancesDbContext>>();
                services.AddSingleton(
                    new DbContextOptionsBuilder<HouseholdFinancesDbContext>()
                        .UseInMemoryDatabase(_databaseName)
                        .Options);

                // Authenticate the test principal instead of the deferred no-op scheme.
                services.AddAuthentication(TestAuthenticationHandler.SchemeName)
                    .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                        TestAuthenticationHandler.SchemeName,
                        _ => { });
            });
        }

        public void Seed()
        {
            using var scope = Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<HouseholdFinancesDbContext>();

            context.Households.Add(new Household { Id = HouseholdId, Name = "Brown Household" });
            context.Users.Add(new User
            {
                Id = UserId,
                Name = "Alex Doe",
                Email = "alex@example.com",
                Password = "unused-hash"
            });
            context.UserHouseholds.Add(new UserHousehold
            {
                Id = Guid.NewGuid(),
                UserId = UserId,
                HouseholdId = HouseholdId
            });
            context.SaveChanges();
        }
    }

    /// <summary>
    /// Test-only authentication scheme. A request carrying the test header authenticates as the
    /// supplied user id; a request without it is treated as anonymous, so the default
    /// authenticated-user requirement can be exercised end to end.
    /// </summary>
    public sealed class TestAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public const string SchemeName = "Test";
        public const string HeaderName = "X-Test-User";

        public TestAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(HeaderName, out var values)
                || values.Count == 0
                || string.IsNullOrWhiteSpace(values[0]))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var claims = new[] { new Claim(ClaimTypes.NameIdentifier, values[0]!) };
            var identity = new ClaimsIdentity(claims, SchemeName);
            var principal = new ClaimsPrincipal(identity);

            return Task.FromResult(
                AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
        }
    }
}
