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
        Assert.Equal(0, root.GetProperty("incomeEntryCount").GetInt32());
        Assert.Equal(0, root.GetProperty("billEntryCount").GetInt32());
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
                StartDate = new DateTime(2026, 10, 5),
                EndDate = new DateTime(2026, 10, 31),
                UserId = UserId
            });
            context.Bills.Add(new Bill
            {
                Id = Guid.NewGuid(),
                Name = "Rent",
                Value = 1500.00,
                StartDate = new DateTime(2026, 10, 5),
                EndDate = new DateTime(2026, 10, 31),
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
    public async Task GetById_WithoutPeriod_TotalsTheCurrentMonthAsIfMonthlyWereSent()
    {
        using var factory = new Factory();

        var householdId = Guid.NewGuid();
        Seed(factory, context =>
        {
            context.Households.Add(new Household { Id = householdId, Name = "Test Household" });
            context.UserHouseholds.Add(new UserHousehold
            {
                Id = Guid.NewGuid(),
                UserId = UserId,
                HouseholdId = householdId
            });

            // One occurrence inside the pinned month and one outside it. Only the first may count.
            context.Incomes.Add(new Income
            {
                Id = Guid.NewGuid(),
                Name = "In month",
                Value = 100.00,
                StartDate = new DateTime(2026, 10, 5),
                EndDate = new DateTime(2026, 10, 31),
                UserId = UserId
            });
            context.Incomes.Add(new Income
            {
                Id = Guid.NewGuid(),
                Name = "Previous month",
                Value = 999.00,
                StartDate = new DateTime(2026, 9, 5),
                EndDate = new DateTime(2026, 9, 30),
                UserId = UserId
            });
        });

        using var client = CreateAuthenticatedClient(factory, UserId);

        using var omitted = await client.GetAsync($"/api/v1/households/{householdId}");
        using var explicitMonthly = await client.GetAsync($"/api/v1/households/{householdId}?period=Monthly");

        Assert.Equal(HttpStatusCode.OK, omitted.StatusCode);
        Assert.Equal(HttpStatusCode.OK, explicitMonthly.StatusCode);

        static double IncomesOf(string json) =>
            JsonDocument.Parse(json).RootElement.GetProperty("incomes").GetDouble();

        var omittedIncomes = IncomesOf(await omitted.Content.ReadAsStringAsync());
        var monthlyIncomes = IncomesOf(await explicitMonthly.Content.ReadAsStringAsync());

        // Omitting the parameter is the same request as asking for Monthly, and the out-of-window
        // entry is excluded rather than added at face value.
        Assert.Equal(100.00, omittedIncomes);
        Assert.Equal(omittedIncomes, monthlyIncomes);
    }

    [Theory]
    [InlineData("Fortnightly")]
    [InlineData("3")]
    [InlineData("-1")]
    public async Task GetById_WithAnInvalidPeriod_ReturnsInvalidInputProblem(string period)
    {
        using var factory = new Factory();

        var householdId = Guid.NewGuid();
        Seed(factory, context =>
        {
            context.Households.Add(new Household { Id = householdId, Name = "Test Household" });
            context.UserHouseholds.Add(new UserHousehold
            {
                Id = Guid.NewGuid(),
                UserId = UserId,
                HouseholdId = householdId
            });
        });

        using var client = CreateAuthenticatedClient(factory, UserId);

        using var response = await client.GetAsync(
            $"/api/v1/households/{householdId}?period={period}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;

        // The failure uses the shared error convention and names the field, and a numeric value is
        // rejected too: the wire format is the member name, so "3" must not silently mean Monthly.
        Assert.Equal("InvalidInput", root.GetProperty("errorName").GetString());
        Assert.Equal("Period", root.GetProperty("identifier").GetString());
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

        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal(1, root.GetProperty("errorCode").GetInt32());
        Assert.Equal("NotFound", root.GetProperty("errorName").GetString());
        Assert.Equal(householdId.ToString(), root.GetProperty("identifier").GetString());

        // Counts are part of the household read model, so a non-member must not see them either.
        Assert.DoesNotContain("incomeEntryCount", json, StringComparison.Ordinal);
        Assert.DoesNotContain("billEntryCount", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetById_ReportsEntryCountsThatDoNotDependOnThePeriod()
    {
        using var factory = new Factory();

        var householdId = Guid.NewGuid();
        Seed(factory, context =>
        {
            context.Households.Add(new Household { Id = householdId, Name = "Test Household" });
            context.UserHouseholds.Add(new UserHousehold
            {
                Id = Guid.NewGuid(),
                UserId = UserId,
                HouseholdId = householdId
            });

            // Every entry is dated 2025, so none can produce an occurrence in the pinned 2026
            // windows. The totals are therefore zero for every period while the counts are not.
            context.Incomes.AddRange(
                new Income
                {
                    Id = Guid.NewGuid(),
                    Name = "Old salary",
                    Value = 1000.00,
                    StartDate = new DateTime(2025, 1, 1),
                    EndDate = new DateTime(2025, 12, 31),
                    Recurring = true,
                    Interval = Interval.Monthly,
                    UserId = UserId
                },
                new Income
                {
                    Id = Guid.NewGuid(),
                    Name = "Old bonus",
                    Value = 500.00,
                    StartDate = new DateTime(2025, 6, 1),
                    EndDate = new DateTime(2025, 6, 1),
                    UserId = UserId
                });
            context.Bills.AddRange(
                new Bill
                {
                    Id = Guid.NewGuid(),
                    Name = "Old rent",
                    Value = 900.00,
                    StartDate = new DateTime(2025, 1, 1),
                    EndDate = new DateTime(2025, 12, 31),
                    Recurring = true,
                    Interval = Interval.Monthly,
                    HouseholdId = householdId,
                    UserId = UserId
                },
                new Bill
                {
                    Id = Guid.NewGuid(),
                    Name = "Old insurance",
                    Value = 120.00,
                    StartDate = new DateTime(2025, 7, 1),
                    EndDate = new DateTime(2025, 7, 1),
                    HouseholdId = householdId,
                    UserId = UserId
                });
        });

        using var client = CreateAuthenticatedClient(factory, UserId);

        using var daily = await client.GetAsync($"/api/v1/households/{householdId}?period=Daily");
        using var yearly = await client.GetAsync($"/api/v1/households/{householdId}?period=Yearly");

        Assert.Equal(HttpStatusCode.OK, daily.StatusCode);
        Assert.Equal(HttpStatusCode.OK, yearly.StatusCode);

        using var dailyDocument = JsonDocument.Parse(await daily.Content.ReadAsStringAsync());
        using var yearlyDocument = JsonDocument.Parse(await yearly.Content.ReadAsStringAsync());
        var dailyRoot = dailyDocument.RootElement;
        var yearlyRoot = yearlyDocument.RootElement;

        // This is the case the windowed totals cannot express: totals zero, entries present.
        Assert.Equal(0d, dailyRoot.GetProperty("incomes").GetDouble());
        Assert.Equal(0d, dailyRoot.GetProperty("payments").GetDouble());
        Assert.Equal(0d, yearlyRoot.GetProperty("incomes").GetDouble());
        Assert.Equal(0d, yearlyRoot.GetProperty("payments").GetDouble());

        Assert.Equal(2, dailyRoot.GetProperty("incomeEntryCount").GetInt32());
        Assert.Equal(2, dailyRoot.GetProperty("billEntryCount").GetInt32());
        Assert.Equal(
            dailyRoot.GetProperty("incomeEntryCount").GetInt32(),
            yearlyRoot.GetProperty("incomeEntryCount").GetInt32());
        Assert.Equal(
            dailyRoot.GetProperty("billEntryCount").GetInt32(),
            yearlyRoot.GetProperty("billEntryCount").GetInt32());
    }

    [Fact]
    public async Task List_AndGetById_AgreeOnTheEntryCounts()
    {
        using var factory = new Factory();

        var householdId = Guid.NewGuid();
        Seed(factory, context =>
        {
            context.Households.Add(new Household { Id = householdId, Name = "Test Household" });
            context.UserHouseholds.Add(new UserHousehold
            {
                Id = Guid.NewGuid(),
                UserId = UserId,
                HouseholdId = householdId
            });
            context.Incomes.AddRange(
                new Income
                {
                    Id = Guid.NewGuid(),
                    Name = "Old salary",
                    Value = 1000.00,
                    StartDate = new DateTime(2025, 1, 1),
                    EndDate = new DateTime(2025, 12, 31),
                    Recurring = true,
                    Interval = Interval.Monthly,
                    UserId = UserId
                });
            context.Bills.AddRange(
                new Bill
                {
                    Id = Guid.NewGuid(),
                    Name = "Old rent",
                    Value = 900.00,
                    StartDate = new DateTime(2025, 1, 1),
                    EndDate = new DateTime(2025, 12, 31),
                    HouseholdId = householdId,
                    UserId = UserId
                },
                new Bill
                {
                    Id = Guid.NewGuid(),
                    Name = "Old insurance",
                    Value = 120.00,
                    StartDate = new DateTime(2025, 7, 1),
                    EndDate = new DateTime(2025, 7, 1),
                    HouseholdId = householdId,
                    UserId = UserId
                },
                new Bill
                {
                    Id = Guid.NewGuid(),
                    Name = "Old subscription",
                    Value = 15.00,
                    StartDate = new DateTime(2025, 2, 1),
                    EndDate = new DateTime(2025, 2, 1),
                    HouseholdId = householdId,
                    UserId = UserId
                });
        });

        using var client = CreateAuthenticatedClient(factory, UserId);

        using var list = await client.GetAsync("/api/v1/households?period=Daily");
        using var byId = await client.GetAsync($"/api/v1/households/{householdId}?period=Yearly");

        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Equal(HttpStatusCode.OK, byId.StatusCode);

        using var listDocument = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
        using var byIdDocument = JsonDocument.Parse(await byId.Content.ReadAsStringAsync());
        var listItem = Assert.Single(listDocument.RootElement.EnumerateArray().ToList());
        var byIdRoot = byIdDocument.RootElement;

        Assert.Equal(1, listItem.GetProperty("incomeEntryCount").GetInt32());
        Assert.Equal(3, listItem.GetProperty("billEntryCount").GetInt32());
        Assert.Equal(
            listItem.GetProperty("incomeEntryCount").GetInt32(),
            byIdRoot.GetProperty("incomeEntryCount").GetInt32());
        Assert.Equal(
            listItem.GetProperty("billEntryCount").GetInt32(),
            byIdRoot.GetProperty("billEntryCount").GetInt32());
    }

    [Fact]
    public async Task List_ReportsZeroEntryCountsForAHouseholdWithNoEntries()
    {
        using var factory = new Factory();

        var householdId = Guid.NewGuid();
        Seed(factory, context =>
        {
            context.Households.Add(new Household { Id = householdId, Name = "Test Household" });
            context.UserHouseholds.Add(new UserHousehold
            {
                Id = Guid.NewGuid(),
                UserId = UserId,
                HouseholdId = householdId
            });
        });

        using var client = CreateAuthenticatedClient(factory, UserId);

        using var response = await client.GetAsync("/api/v1/households?period=Yearly");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var item = Assert.Single(document.RootElement.EnumerateArray().ToList());

        Assert.Equal(0d, item.GetProperty("incomes").GetDouble());
        Assert.Equal(0d, item.GetProperty("payments").GetDouble());
        Assert.Equal(0, item.GetProperty("incomeEntryCount").GetInt32());
        Assert.Equal(0, item.GetProperty("billEntryCount").GetInt32());
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
        /// <summary>
        /// The instant every hosted test instance treats as now. Calendar windows are aligned to it,
        /// so pinning it is what makes these tests deterministic: without a pinned clock the same
        /// seeded entries would fall out of the window as the wall clock advanced, and the assertions
        /// would silently change meaning.
        /// </summary>
        public static readonly DateTimeOffset Now = new(2026, 10, 14, 0, 0, 0, TimeSpan.Zero);

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

                // The host registers TimeProvider.System. Replacing it keeps the production service
                // shape while removing the wall-clock dependency from the window assertions.
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(new FixedTimeProvider(Now));
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
