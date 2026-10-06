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
/// Exercises the Household HTTP surface end to end: create, list, and get by id, including the
/// authorization rules and the error convention. Live HTTP calls require authentication, so the
/// host is given a test authentication scheme instead of the deferred no-op scheme.
/// </summary>
public class HouseholdsApiTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherUserId = Guid.Parse("99999999-9999-9999-9999-999999999999");

    [Fact]
    public async Task Create_WithAuthentication_ReturnsCreatedAndCreatesTheMembership()
    {
        using var factory = new Factory();
        using var client = CreateAuthenticatedClient(factory, UserId);

        using var response = await client.PostAsJsonAsync(
            "/api/v1/households",
            new { name = "Brown Household" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        var id = root.GetProperty("id").GetGuid();

        Assert.Equal("Brown Household", root.GetProperty("name").GetString());
        Assert.Equal(0d, root.GetProperty("incomes").GetDouble());
        Assert.Equal(0d, root.GetProperty("payments").GetDouble());
        Assert.Equal($"/api/v1/households/{id}", response.Headers.Location?.AbsolutePath);

        var membership = Query(factory, context => context.UserHouseholds
            .SingleOrDefault(row => row.HouseholdId == id));

        Assert.NotNull(membership);
        Assert.Equal(UserId, membership!.UserId);
    }

    [Fact]
    public async Task Create_WithoutAuthentication_ReturnsUnauthorized()
    {
        using var factory = new Factory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/v1/households",
            new { name = "Brown Household" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithABlankName_ReturnsInvalidInputProblem()
    {
        using var factory = new Factory();
        using var client = CreateAuthenticatedClient(factory, UserId);

        using var response = await client.PostAsJsonAsync(
            "/api/v1/households",
            new { name = "   " });

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
    public async Task List_ReturnsOnlyTheCurrentUsersHouseholds()
    {
        using var factory = new Factory();

        var mine = Guid.NewGuid();
        var theirs = Guid.NewGuid();
        Seed(factory, context =>
        {
            context.Households.AddRange(
                new Household { Id = mine, Name = "Brown Household" },
                new Household { Id = theirs, Name = "Other Household" });
            context.UserHouseholds.AddRange(
                new UserHousehold { Id = Guid.NewGuid(), UserId = UserId, HouseholdId = mine },
                new UserHousehold { Id = Guid.NewGuid(), UserId = OtherUserId, HouseholdId = theirs });
        });

        using var client = CreateAuthenticatedClient(factory, UserId);

        using var response = await client.GetAsync("/api/v1/households");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var item = Assert.Single(document.RootElement.EnumerateArray().ToList());

        Assert.Equal(mine, item.GetProperty("id").GetGuid());
        Assert.Equal("Brown Household", item.GetProperty("name").GetString());
    }

    [Fact]
    public async Task List_WithoutAuthentication_ReturnsUnauthorized()
    {
        using var factory = new Factory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/v1/households");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetById_ForAMember_ReturnsTheHouseholdWithComputedTotals()
    {
        using var factory = new Factory();

        var householdId = Guid.NewGuid();
        Seed(factory, context =>
        {
            context.Households.Add(new Household { Id = householdId, Name = "Brown Household" });
            context.UserHouseholds.Add(new UserHousehold
            {
                Id = Guid.NewGuid(),
                UserId = UserId,
                HouseholdId = householdId
            });
            context.Incomes.Add(new Income
            {
                Id = Guid.NewGuid(),
                Name = "Salary",
                Value = 2250.50,
                UserId = UserId
            });
            context.Bills.Add(new Bill
            {
                Id = Guid.NewGuid(),
                Name = "Rent",
                Value = 1500.00,
                HouseholdId = householdId,
                UserId = UserId
            });
        });

        using var client = CreateAuthenticatedClient(factory, UserId);

        using var response = await client.GetAsync($"/api/v1/households/{householdId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;

        Assert.Equal(householdId, root.GetProperty("id").GetGuid());
        Assert.Equal("Brown Household", root.GetProperty("name").GetString());
        Assert.Equal(2250.50, root.GetProperty("incomes").GetDouble());
        Assert.Equal(1500.00, root.GetProperty("payments").GetDouble());
    }

    [Fact]
    public async Task GetById_ForANonMember_ReturnsNotFoundProblem()
    {
        using var factory = new Factory();

        var householdId = Guid.NewGuid();
        Seed(factory, context =>
        {
            context.Households.Add(new Household { Id = householdId, Name = "Other Household" });
            context.UserHouseholds.Add(new UserHousehold
            {
                Id = Guid.NewGuid(),
                UserId = OtherUserId,
                HouseholdId = householdId
            });
        });

        using var client = CreateAuthenticatedClient(factory, UserId);

        using var response = await client.GetAsync($"/api/v1/households/{householdId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;

        Assert.Equal(1, root.GetProperty("errorCode").GetInt32());
        Assert.Equal("NotFound", root.GetProperty("errorName").GetString());
        Assert.Equal(householdId.ToString(), root.GetProperty("identifier").GetString());
    }

    private static HttpClient CreateAuthenticatedClient(Factory factory, Guid userId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.HeaderName, userId.ToString());
        return client;
    }

    private static void Seed(Factory factory, Action<HouseholdFinancesDbContext> seed)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<HouseholdFinancesDbContext>();
        seed(context);
        context.SaveChanges();
    }

    private static T Query<T>(Factory factory, Func<HouseholdFinancesDbContext, T> query)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<HouseholdFinancesDbContext>();
        return query(context);
    }

    /// <summary>
    /// Hosts the API with an isolated in-memory database and a test authentication scheme that
    /// authenticates the user id carried by a request header.
    /// </summary>
    public sealed class Factory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = $"households-tests-{Guid.NewGuid()}";

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
