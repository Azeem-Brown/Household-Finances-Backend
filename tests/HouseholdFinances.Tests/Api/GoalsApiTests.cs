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
/// Exercises the Goal HTTP surface end to end: create, list, and update, including the household
/// scoping and authorization rules, the error convention, the contribution total default, and the
/// nullable interval round-trip. Live HTTP calls require authentication, so the host is given a test
/// authentication scheme instead of the deferred no-op scheme.
/// </summary>
public class GoalsApiTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid MemberUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid OtherUserId = Guid.Parse("99999999-9999-9999-9999-999999999999");
    private static readonly DateTime Start = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime End = new(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Create_WithAuthentication_ReturnsCreatedAndTheGoalAppearsInTheList()
    {
        using var factory = new Factory();
        var householdId = Guid.NewGuid();
        Seed(factory, context => AddHousehold(context, householdId, UserId));

        using var client = CreateAuthenticatedClient(factory, UserId);

        using var response = await client.PostAsJsonAsync(
            $"/api/v1/households/{householdId}/goals",
            new { name = "Emergency Fund", value = 10000.00, startDate = Start, endDate = End, recurring = true, interval = (int)Interval.Monthly, total = 0 });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        var id = root.GetProperty("id").GetGuid();

        Assert.Equal("Emergency Fund", root.GetProperty("name").GetString());
        Assert.Equal(10000.00, root.GetProperty("value").GetDouble());
        Assert.Equal(Start, root.GetProperty("startDate").GetDateTime());
        Assert.Equal(End, root.GetProperty("endDate").GetDateTime());
        Assert.True(root.GetProperty("recurring").GetBoolean());
        Assert.Equal((int)Interval.Monthly, root.GetProperty("interval").GetInt32());
        Assert.Equal(householdId, root.GetProperty("householdId").GetGuid());
        Assert.Equal(UserId, root.GetProperty("userId").GetGuid());
        Assert.Equal(0.00, root.GetProperty("total").GetDouble());
        Assert.Equal($"/api/v1/households/{householdId}/goals/{id}", response.Headers.Location?.OriginalString);

        using var listResponse = await client.GetAsync($"/api/v1/households/{householdId}/goals");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        using var listDocument = JsonDocument.Parse(await listResponse.Content.ReadAsStringAsync());
        var goal = Assert.Single(listDocument.RootElement.EnumerateArray().ToList());
        Assert.Equal(id, goal.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Create_WithASuppliedTotal_DefaultsTheTotalToZero()
    {
        using var factory = new Factory();
        var householdId = Guid.NewGuid();
        Seed(factory, context => AddHousehold(context, householdId, UserId));

        using var client = CreateAuthenticatedClient(factory, UserId);

        using var response = await client.PostAsJsonAsync(
            $"/api/v1/households/{householdId}/goals",
            new { name = "Emergency Fund", value = 10000.00, startDate = Start, endDate = End, recurring = false, total = 1234.56 });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(0.00, document.RootElement.GetProperty("total").GetDouble());
    }

    [Fact]
    public async Task Create_WithoutAuthentication_ReturnsUnauthorized()
    {
        using var factory = new Factory();
        var householdId = Guid.NewGuid();
        Seed(factory, context => AddHousehold(context, householdId, UserId));

        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            $"/api/v1/households/{householdId}/goals",
            new { name = "Emergency Fund", value = 10000.00, startDate = Start, endDate = End, recurring = false });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithABlankName_ReturnsInvalidInputProblem()
    {
        using var factory = new Factory();
        var householdId = Guid.NewGuid();
        Seed(factory, context => AddHousehold(context, householdId, UserId));

        using var client = CreateAuthenticatedClient(factory, UserId);

        using var response = await client.PostAsJsonAsync(
            $"/api/v1/households/{householdId}/goals",
            new { name = "   ", value = 10000.00, startDate = Start, endDate = End, recurring = false });

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
    public async Task Create_WithANegativeValue_ReturnsInvalidInputProblemForValue()
    {
        using var factory = new Factory();
        var householdId = Guid.NewGuid();
        Seed(factory, context => AddHousehold(context, householdId, UserId));

        using var client = CreateAuthenticatedClient(factory, UserId);

        using var response = await client.PostAsJsonAsync(
            $"/api/v1/households/{householdId}/goals",
            new { name = "Emergency Fund", value = -0.01, startDate = Start, endDate = End, recurring = false });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Value", document.RootElement.GetProperty("identifier").GetString());
    }

    [Fact]
    public async Task Create_WithAnEndDateBeforeTheStartDate_ReturnsInvalidInputProblemForEndDate()
    {
        using var factory = new Factory();
        var householdId = Guid.NewGuid();
        Seed(factory, context => AddHousehold(context, householdId, UserId));

        using var client = CreateAuthenticatedClient(factory, UserId);

        using var response = await client.PostAsJsonAsync(
            $"/api/v1/households/{householdId}/goals",
            new { name = "Emergency Fund", value = 10000.00, startDate = End, endDate = Start, recurring = false });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("EndDate", document.RootElement.GetProperty("identifier").GetString());
    }

    [Fact]
    public async Task Create_RecurringWithoutAnInterval_ReturnsInvalidInputProblem()
    {
        using var factory = new Factory();
        var householdId = Guid.NewGuid();
        Seed(factory, context => AddHousehold(context, householdId, UserId));

        using var client = CreateAuthenticatedClient(factory, UserId);

        using var response = await client.PostAsJsonAsync(
            $"/api/v1/households/{householdId}/goals",
            new { name = "Emergency Fund", value = 10000.00, startDate = Start, endDate = End, recurring = true, interval = (int?)null });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Interval", document.RootElement.GetProperty("identifier").GetString());
    }

    [Fact]
    public async Task Create_NonRecurringWithoutAnInterval_ReturnsNullInterval()
    {
        using var factory = new Factory();
        var householdId = Guid.NewGuid();
        Seed(factory, context => AddHousehold(context, householdId, UserId));

        using var client = CreateAuthenticatedClient(factory, UserId);

        using var response = await client.PostAsJsonAsync(
            $"/api/v1/households/{householdId}/goals",
            new { name = "One-off deposit", value = 500.00, startDate = Start, endDate = End, recurring = false, interval = (int?)null });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"interval\":null", json);

        using var document = JsonDocument.Parse(json);
        Assert.False(document.RootElement.GetProperty("recurring").GetBoolean());
        Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("interval").ValueKind);

        // Round-trip: the stored one-off goal is returned with a null interval by the list endpoint.
        using var listResponse = await client.GetAsync($"/api/v1/households/{householdId}/goals");
        using var listDocument = JsonDocument.Parse(await listResponse.Content.ReadAsStringAsync());
        var goal = Assert.Single(listDocument.RootElement.EnumerateArray().ToList());
        Assert.Equal(JsonValueKind.Null, goal.GetProperty("interval").ValueKind);
    }

    [Fact]
    public async Task Update_RecurringGoalChangedToOneOff_ReturnsNullInterval()
    {
        using var factory = new Factory();
        var householdId = Guid.NewGuid();
        var goalId = Guid.NewGuid();
        Seed(factory, context =>
        {
            AddHousehold(context, householdId, UserId);
            context.Goals.Add(new Goal
            {
                Id = goalId,
                Name = "Emergency Fund",
                Value = 10000.00,
                StartDate = Start,
                EndDate = End,
                Recurring = true,
                Interval = Interval.Monthly,
                HouseholdId = householdId,
                UserId = UserId,
                Total = 500.00
            });
        });

        using var client = CreateAuthenticatedClient(factory, UserId);

        using var response = await client.PutAsJsonAsync(
            $"/api/v1/households/{householdId}/goals/{goalId}",
            new { name = "One-off deposit", value = 500.00, startDate = Start, endDate = End, recurring = false, interval = (int?)null, total = 500.00 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"interval\":null", json);

        using var document = JsonDocument.Parse(json);
        Assert.False(document.RootElement.GetProperty("recurring").GetBoolean());
        Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("interval").ValueKind);
    }

    [Fact]
    public async Task Create_ForANonMember_ReturnsNotFoundProblem()
    {
        using var factory = new Factory();
        var householdId = Guid.NewGuid();
        Seed(factory, context => AddHousehold(context, householdId, OtherUserId));

        using var client = CreateAuthenticatedClient(factory, UserId);

        using var response = await client.PostAsJsonAsync(
            $"/api/v1/households/{householdId}/goals",
            new { name = "Emergency Fund", value = 10000.00, startDate = Start, endDate = End, recurring = false });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;

        Assert.Equal("NotFound", root.GetProperty("errorName").GetString());
        Assert.Equal(householdId.ToString(), root.GetProperty("identifier").GetString());
    }

    [Fact]
    public async Task List_ReturnsEveryMembersGoalsForTheHousehold()
    {
        using var factory = new Factory();
        var householdId = Guid.NewGuid();
        Seed(factory, context =>
        {
            AddHousehold(context, householdId, UserId);
            context.UserHouseholds.Add(new UserHousehold
            {
                Id = Guid.NewGuid(),
                HouseholdId = householdId,
                UserId = MemberUserId
            });
            context.Goals.AddRange(
                new Goal { Id = Guid.NewGuid(), Name = "Holiday", Value = 3000.00, StartDate = Start, EndDate = End, HouseholdId = householdId, UserId = UserId },
                new Goal { Id = Guid.NewGuid(), Name = "Car", Value = 8000.00, StartDate = Start, EndDate = Start, HouseholdId = householdId, UserId = MemberUserId });
        });

        using var client = CreateAuthenticatedClient(factory, UserId);

        using var response = await client.GetAsync($"/api/v1/households/{householdId}/goals");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var goals = document.RootElement.EnumerateArray().ToList();

        Assert.Equal(2, goals.Count);
        Assert.Contains(goals, goal => goal.GetProperty("userId").GetGuid() == MemberUserId);
    }

    [Fact]
    public async Task List_DoesNotReturnAnotherHouseholdsGoals()
    {
        using var factory = new Factory();
        var householdId = Guid.NewGuid();
        var otherHouseholdId = Guid.NewGuid();
        Seed(factory, context =>
        {
            AddHousehold(context, householdId, UserId);
            AddHousehold(context, otherHouseholdId, UserId);
            context.Goals.AddRange(
                new Goal { Id = Guid.NewGuid(), Name = "Mine", Value = 3000.00, StartDate = Start, EndDate = End, HouseholdId = householdId, UserId = UserId },
                new Goal { Id = Guid.NewGuid(), Name = "Theirs", Value = 8000.00, StartDate = Start, EndDate = Start, HouseholdId = otherHouseholdId, UserId = UserId });
        });

        using var client = CreateAuthenticatedClient(factory, UserId);

        using var response = await client.GetAsync($"/api/v1/households/{householdId}/goals");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var goal = Assert.Single(document.RootElement.EnumerateArray().ToList());
        Assert.Equal("Mine", goal.GetProperty("name").GetString());
    }

    [Fact]
    public async Task List_ForANonMember_ReturnsNotFoundProblem()
    {
        using var factory = new Factory();
        var householdId = Guid.NewGuid();
        Seed(factory, context => AddHousehold(context, householdId, OtherUserId));

        using var client = CreateAuthenticatedClient(factory, UserId);

        using var response = await client.GetAsync($"/api/v1/households/{householdId}/goals");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_ChangesTheGoalIncludingTotalAndTheListReflectsIt()
    {
        using var factory = new Factory();
        var householdId = Guid.NewGuid();
        var goalId = Guid.NewGuid();
        Seed(factory, context =>
        {
            AddHousehold(context, householdId, UserId);
            context.UserHouseholds.Add(new UserHousehold
            {
                Id = Guid.NewGuid(),
                HouseholdId = householdId,
                UserId = MemberUserId
            });
            context.Goals.Add(new Goal
            {
                Id = goalId,
                Name = "Old",
                Value = 100.00,
                StartDate = Start,
                EndDate = End,
                HouseholdId = householdId,
                UserId = MemberUserId,
                Total = 0
            });
        });

        using var client = CreateAuthenticatedClient(factory, UserId);

        using var response = await client.PutAsJsonAsync(
            $"/api/v1/households/{householdId}/goals/{goalId}",
            new { name = "Updated", value = 12000.50, startDate = Start, endDate = End, recurring = true, interval = (int)Interval.Yearly, total = 2500.00 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        Assert.Equal("Updated", root.GetProperty("name").GetString());
        Assert.Equal(12000.50, root.GetProperty("value").GetDouble());
        Assert.Equal(2500.00, root.GetProperty("total").GetDouble());
        // Attribution and household are immutable: the goal stays in its household and owned by the
        // member who created it.
        Assert.Equal(MemberUserId, root.GetProperty("userId").GetGuid());
        Assert.Equal(householdId, root.GetProperty("householdId").GetGuid());

        using var listResponse = await client.GetAsync($"/api/v1/households/{householdId}/goals");
        using var listDocument = JsonDocument.Parse(await listResponse.Content.ReadAsStringAsync());
        var goal = Assert.Single(listDocument.RootElement.EnumerateArray().ToList());
        Assert.Equal("Updated", goal.GetProperty("name").GetString());
        Assert.Equal(2500.00, goal.GetProperty("total").GetDouble());
    }

    [Fact]
    public async Task Update_ForANonMember_ReturnsNotFoundProblem()
    {
        using var factory = new Factory();
        var householdId = Guid.NewGuid();
        Seed(factory, context => AddHousehold(context, householdId, OtherUserId));

        using var client = CreateAuthenticatedClient(factory, UserId);

        using var response = await client.PutAsJsonAsync(
            $"/api/v1/households/{householdId}/goals/{Guid.NewGuid()}",
            new { name = "Updated", value = 100.00, startDate = Start, endDate = End, recurring = false });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_ForAGoalOutsideTheHousehold_ReturnsNotFoundProblem()
    {
        using var factory = new Factory();
        var householdId = Guid.NewGuid();
        var otherHouseholdId = Guid.NewGuid();
        var foreignGoalId = Guid.NewGuid();
        Seed(factory, context =>
        {
            AddHousehold(context, householdId, UserId);
            AddHousehold(context, otherHouseholdId, UserId);
            context.Goals.Add(new Goal
            {
                Id = foreignGoalId,
                Name = "Theirs",
                Value = 100.00,
                StartDate = Start,
                EndDate = End,
                HouseholdId = otherHouseholdId,
                UserId = UserId
            });
        });

        using var client = CreateAuthenticatedClient(factory, UserId);

        using var response = await client.PutAsJsonAsync(
            $"/api/v1/households/{householdId}/goals/{foreignGoalId}",
            new { name = "Updated", value = 100.00, startDate = Start, endDate = End, recurring = false });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(foreignGoalId.ToString(), document.RootElement.GetProperty("identifier").GetString());
    }

    [Fact]
    public async Task Update_ForAnUnknownGoal_ReturnsNotFoundProblem()
    {
        using var factory = new Factory();
        var householdId = Guid.NewGuid();
        Seed(factory, context => AddHousehold(context, householdId, UserId));

        using var client = CreateAuthenticatedClient(factory, UserId);
        var goalId = Guid.NewGuid();

        using var response = await client.PutAsJsonAsync(
            $"/api/v1/households/{householdId}/goals/{goalId}",
            new { name = "Updated", value = 100.00, startDate = Start, endDate = End, recurring = false });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(goalId.ToString(), document.RootElement.GetProperty("identifier").GetString());
    }

    private static void AddHousehold(HouseholdFinancesDbContext context, Guid householdId, Guid ownerId)
    {
        context.Households.Add(new Household { Id = householdId, Name = "Brown Household" });
        context.UserHouseholds.Add(new UserHousehold
        {
            Id = Guid.NewGuid(),
            HouseholdId = householdId,
            UserId = ownerId
        });
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

    /// <summary>
    /// Hosts the API with an isolated in-memory database and a test authentication scheme that
    /// authenticates the user id carried by a request header.
    /// </summary>
    public sealed class Factory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = $"goals-tests-{Guid.NewGuid()}";

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
