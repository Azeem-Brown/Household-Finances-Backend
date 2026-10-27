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
    private static readonly DateTimeOffset PinnedNow = new(2026, 10, 14, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task CreateAsync_CreatesTheHouseholdAndTheCurrentUsersMembership()
    {
        var (service, repository, _) = CreateService();

        var detail = await service.CreateAsync("  Brown Household  ");

        Assert.NotEqual(Guid.Empty, detail.Id);
        Assert.Equal("Brown Household", detail.Name);
        Assert.Equal(0d, detail.Incomes);
        Assert.Equal(0d, detail.Payments);
        Assert.Equal(0, detail.IncomeEntryCount);
        Assert.Equal(0, detail.BillEntryCount);

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

        var details = await service.ListForCurrentUserAsync(Interval.Monthly);

        Assert.Equal(new[] { "Alpha Household", "Zeta Household" }, details.Select(detail => detail.Name));
        Assert.Equal(2250.50, details[0].Incomes);
        Assert.Equal(1500.00, details[0].Payments);
        Assert.Equal(0d, details[1].Incomes);
    }

    [Fact]
    public async Task ListForCurrentUserAsync_ReturnsEmptyWhenTheUserHasNoMembership()
    {
        var (service, _, _) = CreateService();

        Assert.Empty(await service.ListForCurrentUserAsync(Interval.Monthly));
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsTheHouseholdForAMemberWithTotalsRoundedHalfUp()
    {
        var household = new Household { Id = HouseholdId, Name = "Brown Household" };
        var (service, repository, _) = CreateService(household, isMember: true);
        repository.Totals[HouseholdId] = new HouseholdTotals(5200.126, 3100.124);

        var detail = await service.GetByIdAsync(HouseholdId, Interval.Monthly);

        Assert.Equal(HouseholdId, detail.Id);
        Assert.Equal("Brown Household", detail.Name);
        Assert.Equal(5200.13, detail.Incomes);
        Assert.Equal(3100.12, detail.Payments);
    }

    [Fact]
    public async Task GetByIdAsync_ThrowsNotFoundForANonMemberOrUnknownHousehold()
    {
        var household = new Household { Id = HouseholdId, Name = "Brown Household" };
        var (service, repository, _) = CreateService(household, isMember: false);

        var exception = await Assert.ThrowsAsync<HouseholdFinancesException>(
            () => service.GetByIdAsync(HouseholdId, Interval.Monthly));

        Assert.Equal(ErrorCode.NotFound, exception.ErrorCode);
        Assert.Equal(HouseholdId, exception.Identifier);
        Assert.Equal($"NotFound {HouseholdId}", exception.Message);

        // The count reads are never reached, so nothing about the household's entries can leak to a
        // caller who is not a member.
        Assert.Empty(repository.EntryCountRequests);
    }

    [Fact]
    public async Task GetByIdAsync_ThrowsNotFoundForAnUnknownHousehold()
    {
        var (service, _, _) = CreateService();

        var exception = await Assert.ThrowsAsync<HouseholdFinancesException>(
            () => service.GetByIdAsync(HouseholdId, Interval.Monthly));

        Assert.Equal(ErrorCode.NotFound, exception.ErrorCode);
        Assert.Equal(HouseholdId, exception.Identifier);
    }

    [Fact]
    public async Task GetByIdAsync_ThrowsUnauthorizedWhenTheCurrentUserCannotBeResolved()
    {
        var (service, _, currentUser) = CreateService();
        currentUser.IsAuthenticated = false;

        var exception = await Assert.ThrowsAsync<HouseholdFinancesException>(
            () => service.GetByIdAsync(HouseholdId, Interval.Monthly));

        Assert.Equal(ErrorCode.Unauthorized, exception.ErrorCode);
        Assert.Equal(HouseholdService.CurrentUserIdentifier, exception.Identifier);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsEntryCountsThatDoNotDependOnThePeriod()
    {
        var household = new Household { Id = HouseholdId, Name = "Brown Household" };
        var (service, repository, _) = CreateService(household, isMember: true);

        // The entries exist but produce no occurrences in either window, so both periods total
        // zero while the counts stay non-zero. That is the distinction this change adds.
        repository.Totals[HouseholdId] = new HouseholdTotals(0d, 0d);
        repository.EntryCounts[HouseholdId] = new HouseholdEntryCounts(2, 3);

        var daily = await service.GetByIdAsync(HouseholdId, Interval.Daily);
        var yearly = await service.GetByIdAsync(HouseholdId, Interval.Yearly);

        Assert.Equal(0d, daily.Incomes);
        Assert.Equal(0d, daily.Payments);
        Assert.Equal(2, daily.IncomeEntryCount);
        Assert.Equal(3, daily.BillEntryCount);
        Assert.Equal(daily.IncomeEntryCount, yearly.IncomeEntryCount);
        Assert.Equal(daily.BillEntryCount, yearly.BillEntryCount);

        // Both reads did happen, and they asked for genuinely different windows, so equal counts
        // are not an artefact of the periods collapsing to the same request.
        Assert.Equal(2, repository.TotalRequests.Count);
        Assert.NotEqual(
            repository.TotalRequests[0].Window.Start,
            repository.TotalRequests[1].Window.Start);
        Assert.Equal(new[] { HouseholdId, HouseholdId }, repository.EntryCountRequests);
    }

    [Fact]
    public async Task ListForCurrentUserAsync_ReturnsTheEntryCountsForEachHousehold()
    {
        var first = new Household { Id = Guid.NewGuid(), Name = "Alpha Household" };
        var second = new Household { Id = Guid.NewGuid(), Name = "Zeta Household" };
        var (service, repository, _) = CreateService();
        repository.Households.AddRange(first, second);
        repository.Memberships.AddRange(
            new UserHousehold { Id = Guid.NewGuid(), UserId = UserId, HouseholdId = first.Id },
            new UserHousehold { Id = Guid.NewGuid(), UserId = UserId, HouseholdId = second.Id });
        repository.EntryCounts[first.Id] = new HouseholdEntryCounts(4, 5);

        var details = await service.ListForCurrentUserAsync(Interval.Daily);

        Assert.Equal(4, details[0].IncomeEntryCount);
        Assert.Equal(5, details[0].BillEntryCount);
        Assert.Equal(0, details[1].IncomeEntryCount);
        Assert.Equal(0, details[1].BillEntryCount);
        Assert.Equal(new[] { first.Id, second.Id }, repository.EntryCountRequests);
    }

    [Fact]
    public async Task ListForCurrentUserAsync_UsesTheWindowForTheRequestedPeriod()
    {
        var household = new Household { Id = HouseholdId, Name = "Brown Household" };
        var (service, repository, _) = CreateService(household, isMember: true);

        await service.ListForCurrentUserAsync(Interval.Quarterly);

        var request = Assert.Single(repository.TotalRequests);
        Assert.Equal(HouseholdId, request.HouseholdId);
        Assert.Equal(new DateOnly(2026, 10, 1), request.Window.Start);
        Assert.Equal(new DateOnly(2026, 12, 31), request.Window.End);
    }

    [Fact]
    public async Task GetByIdAsync_UsesTheWindowFromThePinnedClock()
    {
        var household = new Household { Id = HouseholdId, Name = "Brown Household" };
        var (service, repository, _) = CreateService(household, isMember: true);

        await service.GetByIdAsync(HouseholdId, Interval.Yearly);

        var request = Assert.Single(repository.TotalRequests);
        Assert.Equal(new DateOnly(2026, 1, 1), request.Window.Start);
        Assert.Equal(new DateOnly(2026, 12, 31), request.Window.End);
    }

    [Fact]
    public async Task CreateAsync_UsesTheDefaultMonthlyWindow()
    {
        var (service, repository, _) = CreateService();

        await service.CreateAsync("Brown Household");

        var request = Assert.Single(repository.TotalRequests);
        Assert.Equal(new DateOnly(2026, 10, 1), request.Window.Start);
        Assert.Equal(new DateOnly(2026, 10, 31), request.Window.End);
    }

    [Fact]
    public async Task ListForCurrentUserAsync_DerivesTheWindowFromTheInjectedClock()
    {
        var household = new Household { Id = HouseholdId, Name = "Brown Household" };
        var (service, repository, _) = CreateService(
            household,
            isMember: true,
            now: new DateTimeOffset(2026, 2, 10, 23, 30, 0, TimeSpan.Zero));

        await service.ListForCurrentUserAsync(Interval.Monthly);

        var request = Assert.Single(repository.TotalRequests);
        Assert.Equal(new DateOnly(2026, 2, 1), request.Window.Start);
        Assert.Equal(new DateOnly(2026, 2, 28), request.Window.End);
    }

    [Fact]
    public void Constructor_RejectsNullDependencies()
    {
        var timeProvider = new FixedTimeProvider(PinnedNow);

        Assert.Throws<ArgumentNullException>(
            () => new HouseholdService(null!, new StubCurrentUserService(), timeProvider));
        Assert.Throws<ArgumentNullException>(
            () => new HouseholdService(new FakeHouseholdRepository(), null!, timeProvider));
        Assert.Throws<ArgumentNullException>(
            () => new HouseholdService(new FakeHouseholdRepository(), new StubCurrentUserService(), null!));
    }

    private static (HouseholdService Service, FakeHouseholdRepository Repository, StubCurrentUserService CurrentUser)
        CreateService(Household? household = null, bool isMember = false, DateTimeOffset? now = null)
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
        var timeProvider = new FixedTimeProvider(now ?? PinnedNow);

        return (new HouseholdService(repository, currentUser, timeProvider), repository, currentUser);
    }

    [Fact]
    public async Task SetLogoAsync_StoresTheLogoForAMember()
    {
        Household household = new Household { Id = HouseholdId, Name = "Brown Household" };
        (HouseholdService service, FakeHouseholdRepository repository, _) = CreateService(household, isMember: true);
        byte[] content = new byte[] { 1, 2, 3 };

        await service.SetLogoAsync(HouseholdId, content, "image/png");

        HouseholdLogo stored = Assert.Single(repository.Logos.Values);
        Assert.Equal(HouseholdId, stored.HouseholdId);
        Assert.Equal(content, stored.Content);
        Assert.Equal("image/png", stored.ContentType);
        Assert.Equal(1, repository.SaveCount);
    }

    [Fact]
    public async Task SetLogoAsync_ReplacesAnExistingLogoInPlace()
    {
        Household household = new Household { Id = HouseholdId, Name = "Brown Household" };
        (HouseholdService service, FakeHouseholdRepository repository, _) = CreateService(household, isMember: true);

        await service.SetLogoAsync(HouseholdId, new byte[] { 1 }, "image/png");
        await service.SetLogoAsync(HouseholdId, new byte[] { 9, 9 }, "image/jpeg");

        HouseholdLogo stored = Assert.Single(repository.Logos.Values);
        Assert.Equal(new byte[] { 9, 9 }, stored.Content);
        Assert.Equal("image/jpeg", stored.ContentType);
    }

    [Fact]
    public async Task SetLogoAsync_DropsMediaTypeParametersBeforeMatching()
    {
        Household household = new Household { Id = HouseholdId, Name = "Brown Household" };
        (HouseholdService service, FakeHouseholdRepository repository, _) = CreateService(household, isMember: true);

        await service.SetLogoAsync(HouseholdId, new byte[] { 1 }, "image/png; charset=binary");

        HouseholdLogo stored = Assert.Single(repository.Logos.Values);
        Assert.Equal("image/png", stored.ContentType);
    }

    [Theory]
    [MemberData(nameof(InvalidLogoUploads))]
    public async Task SetLogoAsync_RejectsAnInvalidUploadAndKeepsTheExistingLogo(
        byte[] content,
        string? contentType,
        string expectedIdentifier)
    {
        Household household = new Household { Id = HouseholdId, Name = "Brown Household" };
        (HouseholdService service, FakeHouseholdRepository repository, _) = CreateService(household, isMember: true);
        repository.Logos[HouseholdId] = new HouseholdLogo
        {
            HouseholdId = HouseholdId,
            Content = new byte[] { 7 },
            ContentType = "image/png",
            UpdatedAtUtc = DateTime.UtcNow
        };

        HouseholdFinancesException exception = await Assert.ThrowsAsync<HouseholdFinancesException>(
            () => service.SetLogoAsync(HouseholdId, content, contentType));

        Assert.Equal(ErrorCode.InvalidInput, exception.ErrorCode);
        Assert.Equal(expectedIdentifier, exception.Identifier);

        // A rejected upload must never replace a logo the household already has.
        HouseholdLogo stored = Assert.Single(repository.Logos.Values);
        Assert.Equal(new byte[] { 7 }, stored.Content);
        Assert.Equal("image/png", stored.ContentType);
    }

    public static TheoryData<byte[], string?, string> InvalidLogoUploads => new()
    {
        { Array.Empty<byte>(), "image/png", HouseholdService.LogoIdentifier },
        { new byte[] { 1 }, null, HouseholdService.LogoContentTypeIdentifier },
        { new byte[] { 1 }, "application/pdf", HouseholdService.LogoContentTypeIdentifier },
        { new byte[HouseholdService.MaxLogoBytes + 1], "image/png", HouseholdService.LogoSizeIdentifier }
    };

    [Fact]
    public async Task SetLogoAsync_ThrowsNotFoundForANonMember()
    {
        Household household = new Household { Id = HouseholdId, Name = "Brown Household" };
        (HouseholdService service, FakeHouseholdRepository repository, _) = CreateService(household, isMember: false);

        HouseholdFinancesException exception = await Assert.ThrowsAsync<HouseholdFinancesException>(
            () => service.SetLogoAsync(HouseholdId, new byte[] { 1 }, "image/png"));

        Assert.Equal(ErrorCode.NotFound, exception.ErrorCode);
        Assert.Empty(repository.Logos);
    }

    [Fact]
    public async Task GetLogoAsync_ReturnsNullWhenTheHouseholdHasNoLogo()
    {
        Household household = new Household { Id = HouseholdId, Name = "Brown Household" };
        (HouseholdService service, _, _) = CreateService(household, isMember: true);

        Assert.Null(await service.GetLogoAsync(HouseholdId));
    }

    [Fact]
    public async Task GetLogoAsync_ReturnsTheStoredContentAndType()
    {
        Household household = new Household { Id = HouseholdId, Name = "Brown Household" };
        (HouseholdService service, _, _) = CreateService(household, isMember: true);
        byte[] content = new byte[] { 4, 5, 6 };
        await service.SetLogoAsync(HouseholdId, content, "image/webp");

        HouseholdLogoContent? logo = await service.GetLogoAsync(HouseholdId);

        Assert.NotNull(logo);
        Assert.Equal(content, logo!.Content);
        Assert.Equal("image/webp", logo.ContentType);
    }

    [Fact]
    public async Task RemoveLogoAsync_ReportsWhetherALogoWasRemoved()
    {
        Household household = new Household { Id = HouseholdId, Name = "Brown Household" };
        (HouseholdService service, FakeHouseholdRepository repository, _) = CreateService(household, isMember: true);
        await service.SetLogoAsync(HouseholdId, new byte[] { 1 }, "image/png");

        Assert.True(await service.RemoveLogoAsync(HouseholdId));
        Assert.Empty(repository.Logos);
        Assert.False(await service.RemoveLogoAsync(HouseholdId));
    }

    private sealed class FakeHouseholdRepository : IHouseholdRepository
    {
        public List<Household> Households { get; } = new();

        public List<UserHousehold> Memberships { get; } = new();

        public Dictionary<Guid, HouseholdTotals> Totals { get; } = new();

        public Dictionary<Guid, HouseholdEntryCounts> EntryCounts { get; } = new();

        public List<(Guid HouseholdId, PeriodWindow Window)> TotalRequests { get; } = new();

        public List<Guid> EntryCountRequests { get; } = new();

        public Dictionary<Guid, HouseholdLogo> Logos { get; } = new();

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
            PeriodWindow window,
            CancellationToken cancellationToken = default)
        {
            TotalRequests.Add((householdId, window));

            return Task.FromResult(
                Totals.TryGetValue(householdId, out var totals) ? totals : new HouseholdTotals(0d, 0d));
        }

        public Task<HouseholdEntryCounts> GetEntryCountsAsync(
            Guid householdId,
            CancellationToken cancellationToken = default)
        {
            EntryCountRequests.Add(householdId);

            return Task.FromResult(
                EntryCounts.TryGetValue(householdId, out var counts)
                    ? counts
                    : new HouseholdEntryCounts(0, 0));
        }

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

        public Task<HouseholdLogo?> GetLogoAsync(
            Guid householdId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Logos.TryGetValue(householdId, out var logo) ? logo : null);

        public Task UpsertLogoAsync(HouseholdLogo logo, CancellationToken cancellationToken = default)
        {
            Logos[logo.HouseholdId] = logo;
            return Task.CompletedTask;
        }

        public Task<bool> RemoveLogoAsync(Guid householdId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Logos.Remove(householdId));

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
