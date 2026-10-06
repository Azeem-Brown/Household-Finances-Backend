using HouseholdFinances.Domain.Abstractions;
using HouseholdFinances.Domain.Entities;
using HouseholdFinances.Domain.Errors;
using HouseholdFinances.Domain.Models;
using HouseholdFinances.Infrastructure.Services;

namespace HouseholdFinances.Tests.Services;

/// <summary>
/// Verifies the Household service's create/list/get behaviour, the current-user resolution, the
/// computed totals, the duplicate-membership guard, and the error convention.
/// </summary>
public class HouseholdServiceTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid HouseholdId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public async Task CreateAsync_CreatesTheHouseholdAndTheCurrentUsersMembership()
    {
        var (service, repository, _) = CreateService();

        var detail = await service.CreateAsync("  Brown Household  ");

        Assert.NotEqual(Guid.Empty, detail.Id);
        Assert.Equal("Brown Household", detail.Name);
        Assert.Equal(0d, detail.Incomes);
        Assert.Equal(0d, detail.Payments);

        var household = Assert.Single(repository.Households);
        Assert.Equal(detail.Id, household.Id);
        Assert.Equal("Brown Household", household.Name);

        var membership = Assert.Single(repository.Memberships);
        Assert.Equal(UserId, membership.UserId);
        Assert.Equal(household.Id, membership.HouseholdId);
        Assert.Equal(1, repository.SaveCount);

        // Totals are computed on read, never written onto the stored entity.
        Assert.Equal(0d, household.Incomes);
        Assert.Equal(0d, household.Payments);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateAsync_RejectsABlankNameUsingTheErrorConvention(string? name)
    {
        var (service, repository, _) = CreateService();

        var exception = await Assert.ThrowsAsync<HouseholdFinancesException>(() => service.CreateAsync(name));

        Assert.Equal(ErrorCode.InvalidInput, exception.ErrorCode);
        Assert.Equal(HouseholdService.NameIdentifier, exception.Identifier);
        Assert.Equal("InvalidInput Name", exception.Message);
        Assert.Empty(repository.Households);
        Assert.Equal(0, repository.SaveCount);
    }

    [Fact]
    public async Task CreateAsync_ThrowsUnauthorizedWhenTheCurrentUserCannotBeResolved()
    {
        var (service, repository, currentUser) = CreateService();
        currentUser.IsAuthenticated = false;
        currentUser.UserIdentifier = null;

        var exception = await Assert.ThrowsAsync<HouseholdFinancesException>(
            () => service.CreateAsync("Brown Household"));

        Assert.Equal(ErrorCode.Unauthorized, exception.ErrorCode);
        Assert.Equal(HouseholdService.CurrentUserIdentifier, exception.Identifier);
        Assert.Equal(0, repository.SaveCount);
    }

    [Fact]
    public async Task CreateAsync_ThrowsConflictWhenTheMembershipAlreadyExists()
    {
        var (service, repository, _) = CreateService();
        repository.MembershipAlwaysExists = true;

        var exception = await Assert.ThrowsAsync<HouseholdFinancesException>(
            () => service.CreateAsync("Brown Household"));

        Assert.Equal(ErrorCode.Conflict, exception.ErrorCode);
        Assert.Empty(repository.Households);
        Assert.Equal(0, repository.SaveCount);
    }

    [Fact]
    public async Task ListForCurrentUserAsync_ReturnsOnlyTheCurrentUsersHouseholdsWithTotals()
    {
        var first = new Household { Id = Guid.NewGuid(), Name = "Alpha Household" };
        var second = new Household { Id = Guid.NewGuid(), Name = "Zeta Household" };
        var unrelated = new Household { Id = Guid.NewGuid(), Name = "Other Household" };
        var (service, repository, _) = CreateService();
        repository.Households.AddRange(first, second, unrelated);
        repository.Memberships.AddRange(
            new UserHousehold { Id = Guid.NewGuid(), UserId = UserId, HouseholdId = first.Id },
            new UserHousehold { Id = Guid.NewGuid(), UserId = UserId, HouseholdId = second.Id },
            new UserHousehold { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), HouseholdId = unrelated.Id });
        repository.Totals[first.Id] = new HouseholdTotals(2250.50, 1500.00);

        var details = await service.ListForCurrentUserAsync();

        Assert.Equal(new[] { "Alpha Household", "Zeta Household" }, details.Select(detail => detail.Name));
        Assert.Equal(2250.50, details[0].Incomes);
        Assert.Equal(1500.00, details[0].Payments);
        Assert.Equal(0d, details[1].Incomes);
    }

    [Fact]
    public async Task ListForCurrentUserAsync_ReturnsEmptyWhenTheUserHasNoMembership()
    {
        var (service, _, _) = CreateService();

        Assert.Empty(await service.ListForCurrentUserAsync());
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsTheHouseholdForAMemberWithTotalsRoundedHalfUp()
    {
        var household = new Household { Id = HouseholdId, Name = "Brown Household" };
        var (service, repository, _) = CreateService(household, isMember: true);
        repository.Totals[HouseholdId] = new HouseholdTotals(5200.126, 3100.124);

        var detail = await service.GetByIdAsync(HouseholdId);

        Assert.Equal(HouseholdId, detail.Id);
        Assert.Equal("Brown Household", detail.Name);
        Assert.Equal(5200.13, detail.Incomes);
        Assert.Equal(3100.12, detail.Payments);
    }

    [Fact]
    public async Task GetByIdAsync_ThrowsNotFoundForANonMemberOrUnknownHousehold()
    {
        var household = new Household { Id = HouseholdId, Name = "Brown Household" };
        var (service, _, _) = CreateService(household, isMember: false);

        var exception = await Assert.ThrowsAsync<HouseholdFinancesException>(
            () => service.GetByIdAsync(HouseholdId));

        Assert.Equal(ErrorCode.NotFound, exception.ErrorCode);
        Assert.Equal(HouseholdId, exception.Identifier);
        Assert.Equal($"NotFound {HouseholdId}", exception.Message);
    }

    [Fact]
    public async Task GetByIdAsync_ThrowsNotFoundForAnUnknownHousehold()
    {
        var (service, _, _) = CreateService();

        var exception = await Assert.ThrowsAsync<HouseholdFinancesException>(
            () => service.GetByIdAsync(HouseholdId));

        Assert.Equal(ErrorCode.NotFound, exception.ErrorCode);
        Assert.Equal(HouseholdId, exception.Identifier);
    }

    [Fact]
    public async Task GetByIdAsync_ThrowsUnauthorizedWhenTheCurrentUserCannotBeResolved()
    {
        var (service, _, currentUser) = CreateService();
        currentUser.IsAuthenticated = false;

        var exception = await Assert.ThrowsAsync<HouseholdFinancesException>(
            () => service.GetByIdAsync(HouseholdId));

        Assert.Equal(ErrorCode.Unauthorized, exception.ErrorCode);
        Assert.Equal(HouseholdService.CurrentUserIdentifier, exception.Identifier);
    }

    [Fact]
    public void Constructor_RejectsNullDependencies()
    {
        Assert.Throws<ArgumentNullException>(
            () => new HouseholdService(null!, new StubCurrentUserService()));
        Assert.Throws<ArgumentNullException>(
            () => new HouseholdService(new FakeHouseholdRepository(), null!));
    }

    private static (HouseholdService Service, FakeHouseholdRepository Repository, StubCurrentUserService CurrentUser)
        CreateService(Household? household = null, bool isMember = false)
    {
        var repository = new FakeHouseholdRepository();

        if (household is not null)
        {
            repository.Households.Add(household);

            if (isMember)
            {
                repository.Memberships.Add(new UserHousehold
                {
                    Id = Guid.NewGuid(),
                    UserId = UserId,
                    HouseholdId = household.Id
                });
            }
        }

        var currentUser = new StubCurrentUserService();

        return (new HouseholdService(repository, currentUser), repository, currentUser);
    }

    private sealed class FakeHouseholdRepository : IHouseholdRepository
    {
        public List<Household> Households { get; } = new();

        public List<UserHousehold> Memberships { get; } = new();

        public Dictionary<Guid, HouseholdTotals> Totals { get; } = new();

        public bool MembershipAlwaysExists { get; set; }

        public int SaveCount { get; private set; }

        public Task<Household?> GetForMemberAsync(
            Guid householdId,
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            var isMember = Memberships.Any(
                membership => membership.HouseholdId == householdId && membership.UserId == userId);

            return Task.FromResult(
                isMember ? Households.FirstOrDefault(household => household.Id == householdId) : null);
        }

        public Task<IReadOnlyList<Household>> GetByMemberAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            var householdIds = Memberships
                .Where(membership => membership.UserId == userId)
                .Select(membership => membership.HouseholdId)
                .ToHashSet();

            IReadOnlyList<Household> result = Households
                .Where(household => householdIds.Contains(household.Id))
                .OrderBy(household => household.Name, StringComparer.Ordinal)
                .ToList();

            return Task.FromResult(result);
        }

        public Task<bool> MembershipExistsAsync(
            Guid householdId,
            Guid userId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(
                MembershipAlwaysExists
                || Memberships.Any(
                    membership => membership.HouseholdId == householdId && membership.UserId == userId));

        public Task<HouseholdTotals> GetTotalsAsync(
            Guid householdId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(
                Totals.TryGetValue(householdId, out var totals) ? totals : new HouseholdTotals(0d, 0d));

        public Task AddAsync(Household household, CancellationToken cancellationToken = default)
        {
            Households.Add(household);
            return Task.CompletedTask;
        }

        public Task AddMembershipAsync(UserHousehold membership, CancellationToken cancellationToken = default)
        {
            Memberships.Add(membership);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class StubCurrentUserService : ICurrentUserService
    {
        public bool IsAuthenticated { get; set; } = true;

        public string? UserIdentifier { get; set; } = UserId.ToString();
    }
}
