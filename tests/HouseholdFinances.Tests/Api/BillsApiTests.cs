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
/// Exercises the Bill HTTP surface end to end: create, list, update, and delete, including the
/// household scoping and authorization rules and the error convention. Live HTTP calls require
/// authentication, so the host is given a test authentication scheme instead of the deferred no-op
/// scheme.
/// </summary>
public class BillsApiTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid MemberUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid OtherUserId = Guid.Parse("99999999-9999-9999-9999-999999999999");
    private static readonly DateTime Start = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime End = new(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Create_WithAuthentication_ReturnsCreatedAndTheBillAppearsInTheList()
    {
        using var factory = new Factory();
        var householdId = Guid.NewGuid();
        Seed(factory, context => AddHousehold(context, householdId, UserId));

        using var client = CreateAuthenticatedClient(factory, UserId);

        using var response = await client.PostAsJsonAsync(
            $"/api/v1/households/{householdId}/bills",
            new { name = "Rent", value = 1200.00, startDate = Start, endDate = End, recurring = true, interval = (int)Interval.Monthly });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        var id = root.GetProperty("id").GetGuid();

        Assert.Equal("Rent", root.GetProperty("name").GetString());
        Assert.Equal(1200.00, root.GetProperty("value").GetDouble());
        Assert.Equal(Start, root.GetProperty("startDate").GetDateTime());
        Assert.Equal(End, root.GetProperty("endDate").GetDateTime());
        Assert.True(root.GetProperty("recurring").GetBoolean());
        Assert.Equal((int)Interval.Monthly, root.GetProperty("interval").GetInt32());
        Assert.Equal(householdId, root.GetProperty("householdId").GetGuid());
        Assert.Equal(UserId, root.GetProperty("userId").GetGuid());
        Assert.Equal($"/api/v1/households/{householdId}/bills/{id}", response.Headers.Location?.OriginalString);

        using var listResponse = await client.GetAsync($"/api/v1/households/{householdId}/bills");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        using var listDocument = JsonDocument.Parse(await listResponse.Content.ReadAsStringAsync());
        var bill = Assert.Single(listDocument.RootElement.EnumerateArray().ToList());
        Assert.Equal(id, bill.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Create_WithoutAuthentication_ReturnsUnauthorized()
    {
        using var factory = new Factory();
        var householdId = Guid.NewGuid();
        Seed(factory, context => AddHousehold(context, householdId, UserId));

        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            $"/api/v1/households/{householdId}/bills",
            new { name = "Rent", value = 1200.00, startDate = Start, endDate = End, recurring = false });

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
            $"/api/v1/households/{householdId}/bills",
            new { name = "   ", value = 1200.00, startDate = Start, endDate = End, recurring = false });

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
            $"/api/v1/households/{householdId}/bills",
            new { name = "Rent", value = -0.01, startDate = Start, endDate = End, recurring = false });

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
            $"/api/v1/households/{householdId}/bills",
            new { name = "Rent", value = 1200.00, startDate = End, endDate = Start, recurring = false });

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
            $"/api/v1/households/{householdId}/bills",
            new { name = "Rent", value = 1200.00, startDate = Start, endDate = End, recurring = true, interval = (int?)null });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Interval", document.RootElement.GetProperty("identifier").GetString());
    }

    [Fact]
    public async Task Create_ForANonMember_ReturnsNotFoundProblem()
    {
        using var factory = new Factory();
        var householdId = Guid.NewGuid();
        Seed(factory, context => AddHousehold(context, householdId, OtherUserId));

        using var client = CreateAuthenticatedClient(factory, UserId);

        using var response = await client.PostAsJsonAsync(
            $"/api/v1/households/{householdId}/bills",
            new { name = "Rent", value = 1200.00, startDate = Start, endDate = End, recurring = false });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;

        Assert.Equal("NotFound", root.GetProperty("errorName").GetString());
        Assert.Equal(householdId.ToString(), root.GetProperty("identifier").GetString());
    }

    [Fact]
    public async Task List_ReturnsEveryMembersBillsForTheHousehold()
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
            context.Bills.AddRange(
                new Bill { Id = Guid.NewGuid(), Name = "Rent", Value = 1200.00, StartDate = Start, EndDate = End, HouseholdId = householdId, UserId = UserId },
                new Bill { Id = Guid.NewGuid(), Name = "Water", Value = 60.00, StartDate = Start, EndDate = Start, HouseholdId = householdId, UserId = MemberUserId });
        });

        using var client = CreateAuthenticatedClient(factory, UserId);

        using var response = await client.GetAsync($"/api/v1/households/{householdId}/bills");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var bills = document.RootElement.EnumerateArray().ToList();

        Assert.Equal(2, bills.Count);
        Assert.Contains(bills, bill => bill.GetProperty("userId").GetGuid() == MemberUserId);
    }

    [Fact]
    public async Task List_DoesNotReturnAnotherHouseholdsBills()
    {
        using var factory = new Factory();
        var householdId = Guid.NewGuid();
        var otherHouseholdId = Guid.NewGuid();
        Seed(factory, context =>
        {
            AddHousehold(context, householdId, UserId);
            AddHousehold(context, otherHouseholdId, UserId);
            context.Bills.AddRange(
                new Bill { Id = Guid.NewGuid(), Name = "Mine", Value = 1200.00, StartDate = Start, EndDate = End, HouseholdId = householdId, UserId = UserId },
                new Bill { Id = Guid.NewGuid(), Name = "Theirs", Value = 60.00, StartDate = Start, EndDate = Start, HouseholdId = otherHouseholdId, UserId = UserId });
        });

        using var client = CreateAuthenticatedClient(factory, UserId);

        using var response = await client.GetAsync($"/api/v1/households/{householdId}/bills");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var bill = Assert.Single(document.RootElement.EnumerateArray().ToList());
        Assert.Equal("Mine", bill.GetProperty("name").GetString());
    }

    [Fact]
    public async Task List_ForANonMember_ReturnsNotFoundProblem()
    {
        using var factory = new Factory();
        var householdId = Guid.NewGuid();
        Seed(factory, context => AddHousehold(context, householdId, OtherUserId));

        using var client = CreateAuthenticatedClient(factory, UserId);

        using var response = await client.GetAsync($"/api/v1/households/{householdId}/bills");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_ChangesTheBillAndTheListReflectsIt()
    {
        using var factory = new Factory();
        var householdId = Guid.NewGuid();
        var billId = Guid.NewGuid();
        Seed(factory, context =>
        {
            AddHousehold(context, householdId, UserId);
            context.Bills.Add(new Bill
            {
                Id = billId,
                Name = "Old",
                Value = 100.00,
                StartDate = Start,
                EndDate = End,
                HouseholdId = householdId,
                UserId = MemberUserId
            });
            context.UserHouseholds.Add(new UserHousehold
            {
                Id = Guid.NewGuid(),
                HouseholdId = householdId,
                UserId = MemberUserId
            });
        });

        using var client = CreateAuthenticatedClient(factory, UserId);

        using var response = await client.PutAsJsonAsync(
            $"/api/v1/households/{householdId}/bills/{billId}",
            new { name = "Updated", value = 1350.50, startDate = Start, endDate = End, recurring = true, interval = (int)Interval.Yearly });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        Assert.Equal("Updated", root.GetProperty("name").GetString());
        Assert.Equal(1350.50, root.GetProperty("value").GetDouble());
        // Attribution is immutable: the bill stays owned by the member who created it.
        Assert.Equal(MemberUserId, root.GetProperty("userId").GetGuid());
        Assert.Equal(householdId, root.GetProperty("householdId").GetGuid());

        using var listResponse = await client.GetAsync($"/api/v1/households/{householdId}/bills");
        using var listDocument = JsonDocument.Parse(await listResponse.Content.ReadAsStringAsync());
        var bill = Assert.Single(listDocument.RootElement.EnumerateArray().ToList());
        Assert.Equal("Updated", bill.GetProperty("name").GetString());
    }

    [Fact]
    public async Task Update_ForANonMember_ReturnsNotFoundProblem()
    {
        using var factory = new Factory();
        var householdId = Guid.NewGuid();
        Seed(factory, context => AddHousehold(context, householdId, OtherUserId));

        using var client = CreateAuthenticatedClient(factory, UserId);

        using var response = await client.PutAsJsonAsync(
            $"/api/v1/households/{householdId}/bills/{Guid.NewGuid()}",
            new { name = "Updated", value = 100.00, startDate = Start, endDate = End, recurring = false });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_ForABillOutsideTheHousehold_ReturnsNotFoundProblem()
    {
        using var factory = new Factory();
        var householdId = Guid.NewGuid();
        var otherHouseholdId = Guid.NewGuid();
        var foreignBillId = Guid.NewGuid();
        Seed(factory, context =>
        {
            AddHousehold(context, householdId, UserId);
            AddHousehold(context, otherHouseholdId, UserId);
            context.Bills.Add(new Bill
            {
                Id = foreignBillId,
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
            $"/api/v1/households/{householdId}/bills/{foreignBillId}",
            new { name = "Updated", value = 100.00, startDate = Start, endDate = End, recurring = false });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(foreignBillId.ToString(), document.RootElement.GetProperty("identifier").GetString());
    }

    [Fact]
    public async Task Update_ForAnUnknownBill_ReturnsNotFoundProblem()
    {
        using var factory = new Factory();
        var householdId = Guid.NewGuid();
        Seed(factory, context => AddHousehold(context, householdId, UserId));

        using var client = CreateAuthenticatedClient(factory, UserId);
        var billId = Guid.NewGuid();

        using var response = await client.PutAsJsonAsync(
            $"/api/v1/households/{householdId}/bills/{billId}",
            new { name = "Updated", value = 100.00, startDate = Start, endDate = End, recurring = false });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(billId.ToString(), document.RootElement.GetProperty("identifier").GetString());
    }

    [Fact]
    public async Task Delete_RemovesTheBillAndTheListReflectsIt()
    {
        using var factory = new Factory();
        var householdId = Guid.NewGuid();
        var billId = Guid.NewGuid();
        Seed(factory, context =>
        {
            AddHousehold(context, householdId, UserId);
            context.Bills.Add(new Bill
            {
                Id = billId,
                Name = "Rent",
                Value = 1200.00,
                StartDate = Start,
                EndDate = End,
                HouseholdId = householdId,
                UserId = UserId
            });
        });

        using var client = CreateAuthenticatedClient(factory, UserId);

        using var response = await client.DeleteAsync($"/api/v1/households/{householdId}/bills/{billId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var listResponse = await client.GetAsync($"/api/v1/households/{householdId}/bills");
        using var listDocument = JsonDocument.Parse(await listResponse.Content.ReadAsStringAsync());
        Assert.Empty(listDocument.RootElement.EnumerateArray().ToList());
    }

    [Fact]
    public async Task Delete_ForANonMember_ReturnsNotFoundProblem()
    {
        using var factory = new Factory();
        var householdId = Guid.NewGuid();
        Seed(factory, context => AddHousehold(context, householdId, OtherUserId));

        using var client = CreateAuthenticatedClient(factory, UserId);

        using var response = await client.DeleteAsync($"/api/v1/households/{householdId}/bills/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
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
        private readonly string _databaseName = $"bills-tests-{Guid.NewGuid()}";

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
