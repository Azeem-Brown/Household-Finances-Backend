using HouseholdFinances.Domain.Entities;
using HouseholdFinances.Infrastructure.Persistence;
using HouseholdFinances.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace HouseholdFinances.Tests.Repositories;

/// <summary>
/// Verifies the EF Core Household repository against an in-memory provider, so persistence and
/// totals behaviour is covered without a MySQL server.
/// </summary>
public class HouseholdRepositoryTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherUserId = Guid.Parse("99999999-9999-9999-9999-999999999999");

    [Fact]
    public async Task GetForMemberAsync_ReturnsTheHouseholdForAMember()
    {
        await using var context = CreateContext(nameof(GetForMemberAsync_ReturnsTheHouseholdForAMember));
        var household = new Household { Id = Guid.NewGuid(), Name = "Brown Household" };
        context.Households.Add(household);
        context.UserHouseholds.Add(new UserHousehold
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            HouseholdId = household.Id
        });
        await context.SaveChangesAsync();

        var repository = new HouseholdRepository(context);

        var found = await repository.GetForMemberAsync(household.Id, UserId);

        Assert.NotNull(found);
        Assert.Equal(household.Id, found!.Id);
        Assert.Equal("Brown Household", found.Name);
    }

    [Fact]
    public async Task GetForMemberAsync_ReturnsNullForANonMemberOrUnknownHousehold()
    {
        await using var context = CreateContext(nameof(GetForMemberAsync_ReturnsNullForANonMemberOrUnknownHousehold));
        var household = new Household { Id = Guid.NewGuid(), Name = "Brown Household" };
        context.Households.Add(household);
        await context.SaveChangesAsync();

        var repository = new HouseholdRepository(context);

        Assert.Null(await repository.GetForMemberAsync(household.Id, UserId));
        Assert.Null(await repository.GetForMemberAsync(Guid.NewGuid(), UserId));
    }

    [Fact]
    public async Task GetByMemberAsync_ReturnsOnlyTheUsersHouseholdsOrderedByName()
    {
        await using var context = CreateContext(nameof(GetByMemberAsync_ReturnsOnlyTheUsersHouseholdsOrderedByName));
        var first = new Household { Id = Guid.NewGuid(), Name = "Alpha Household" };
        var second = new Household { Id = Guid.NewGuid(), Name = "Zeta Household" };
        var unrelated = new Household { Id = Guid.NewGuid(), Name = "Other Household" };
        context.Households.AddRange(first, second, unrelated);
        context.UserHouseholds.AddRange(
            new UserHousehold { Id = Guid.NewGuid(), UserId = UserId, HouseholdId = second.Id },
            new UserHousehold { Id = Guid.NewGuid(), UserId = UserId, HouseholdId = first.Id },
            new UserHousehold { Id = Guid.NewGuid(), UserId = OtherUserId, HouseholdId = unrelated.Id });
        await context.SaveChangesAsync();

        var repository = new HouseholdRepository(context);

        var households = await repository.GetByMemberAsync(UserId);

        Assert.Equal(new[] { "Alpha Household", "Zeta Household" }, households.Select(h => h.Name));
    }

    [Fact]
    public async Task GetByMemberAsync_ReturnsEmptyForAUserWithNoMembership()
    {
        await using var context = CreateContext(nameof(GetByMemberAsync_ReturnsEmptyForAUserWithNoMembership));
        var repository = new HouseholdRepository(context);

        Assert.Empty(await repository.GetByMemberAsync(UserId));
    }

    [Fact]
    public async Task MembershipExistsAsync_DistinguishesMembersFromNonMembers()
    {
        await using var context = CreateContext(nameof(MembershipExistsAsync_DistinguishesMembersFromNonMembers));
        var householdId = Guid.NewGuid();
        context.UserHouseholds.Add(new UserHousehold
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            HouseholdId = householdId
        });
        await context.SaveChangesAsync();

        var repository = new HouseholdRepository(context);

        Assert.True(await repository.MembershipExistsAsync(householdId, UserId));
        Assert.False(await repository.MembershipExistsAsync(householdId, OtherUserId));
        Assert.False(await repository.MembershipExistsAsync(Guid.NewGuid(), UserId));
    }

    [Fact]
    public async Task GetTotalsAsync_SumsMemberIncomesAndHouseholdBillsOnly()
    {
        await using var context = CreateContext(nameof(GetTotalsAsync_SumsMemberIncomesAndHouseholdBillsOnly));
        var householdId = Guid.NewGuid();
        var otherHouseholdId = Guid.NewGuid();

        context.UserHouseholds.AddRange(
            new UserHousehold { Id = Guid.NewGuid(), UserId = UserId, HouseholdId = householdId },
            new UserHousehold { Id = Guid.NewGuid(), UserId = OtherUserId, HouseholdId = otherHouseholdId });
        context.Incomes.AddRange(
            new Income { Id = Guid.NewGuid(), Name = "Salary", Value = 2000.00, UserId = UserId },
            new Income { Id = Guid.NewGuid(), Name = "Side work", Value = 250.50, UserId = UserId },
            new Income { Id = Guid.NewGuid(), Name = "Other salary", Value = 9999.00, UserId = OtherUserId });
        context.Bills.AddRange(
            new Bill { Id = Guid.NewGuid(), Name = "Rent", Value = 1500.00, HouseholdId = householdId, UserId = UserId },
            new Bill { Id = Guid.NewGuid(), Name = "Other rent", Value = 800.00, HouseholdId = otherHouseholdId, UserId = OtherUserId });
        await context.SaveChangesAsync();

        var repository = new HouseholdRepository(context);

        var totals = await repository.GetTotalsAsync(householdId);

        Assert.Equal(2250.50, totals.Incomes);
        Assert.Equal(1500.00, totals.Payments);
    }

    [Fact]
    public async Task GetTotalsAsync_ReturnsZeroWhenThereAreNoEntries()
    {
        await using var context = CreateContext(nameof(GetTotalsAsync_ReturnsZeroWhenThereAreNoEntries));
        var repository = new HouseholdRepository(context);

        var totals = await repository.GetTotalsAsync(Guid.NewGuid());

        Assert.Equal(0d, totals.Incomes);
        Assert.Equal(0d, totals.Payments);
    }

    [Fact]
    public async Task AddAsync_AndAddMembershipAsync_PersistThroughSaveChanges()
    {
        const string databaseName = nameof(AddAsync_AndAddMembershipAsync_PersistThroughSaveChanges);
        var householdId = Guid.NewGuid();

        await using (var context = CreateContext(databaseName))
        {
            var repository = new HouseholdRepository(context);
            await repository.AddAsync(new Household { Id = householdId, Name = "Brown Household" });
            await repository.AddMembershipAsync(new UserHousehold
            {
                Id = Guid.NewGuid(),
                UserId = UserId,
                HouseholdId = householdId
            });
            await repository.SaveChangesAsync();
        }

        await using (var verifyContext = CreateContext(databaseName))
        {
            Assert.NotNull(await verifyContext.Households.SingleOrDefaultAsync(h => h.Id == householdId));
            Assert.True(await verifyContext.UserHouseholds.AnyAsync(
                membership => membership.HouseholdId == householdId && membership.UserId == UserId));
        }
    }

    [Fact]
    public void Constructor_RejectsANullContext()
    {
        Assert.Throws<ArgumentNullException>(() => new HouseholdRepository(null!));
    }

    [Fact]
    public async Task AddAsync_RejectsANullHousehold()
    {
        await using var context = CreateContext(nameof(AddAsync_RejectsANullHousehold));
        var repository = new HouseholdRepository(context);

        await Assert.ThrowsAsync<ArgumentNullException>(() => repository.AddAsync(null!));
    }

    private static HouseholdFinancesDbContext CreateContext(string databaseName) =>
        new(new DbContextOptionsBuilder<HouseholdFinancesDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options);
}
