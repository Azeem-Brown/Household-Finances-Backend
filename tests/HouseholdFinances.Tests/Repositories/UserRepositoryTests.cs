using HouseholdFinances.Domain.Entities;
using HouseholdFinances.Infrastructure.Persistence;
using HouseholdFinances.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace HouseholdFinances.Tests.Repositories;

/// <summary>
/// Verifies the EF Core User repository against an in-memory provider, so persistence behaviour is
/// covered without a MySQL server.
/// </summary>
public class UserRepositoryTests
{
    [Fact]
    public async Task GetByIdAsync_ReturnsTheMatchingUser()
    {
        await using var context = CreateContext(nameof(GetByIdAsync_ReturnsTheMatchingUser));
        var user = new User { Id = Guid.NewGuid(), Name = "Alex", Email = "alex@example.com" };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var repository = new UserRepository(context);

        var found = await repository.GetByIdAsync(user.Id);

        Assert.NotNull(found);
        Assert.Equal(user.Id, found!.Id);
        Assert.Equal("Alex", found.Name);
        Assert.Equal("alex@example.com", found.Email);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNullForAnUnknownIdentifier()
    {
        await using var context = CreateContext(nameof(GetByIdAsync_ReturnsNullForAnUnknownIdentifier));
        var repository = new UserRepository(context);

        var found = await repository.GetByIdAsync(Guid.NewGuid());

        Assert.Null(found);
    }

    [Fact]
    public async Task GetHouseholdsAsync_ReturnsOnlyTheUsersHouseholdsOrderedByName()
    {
        await using var context = CreateContext(nameof(GetHouseholdsAsync_ReturnsOnlyTheUsersHouseholdsOrderedByName));
        var user = new User { Id = Guid.NewGuid(), Name = "Alex", Email = "alex@example.com" };
        var otherUser = new User { Id = Guid.NewGuid(), Name = "Sam", Email = "sam@example.com" };
        var second = new Household { Id = Guid.NewGuid(), Name = "Zeta Household" };
        var first = new Household { Id = Guid.NewGuid(), Name = "Alpha Household" };
        var unrelated = new Household { Id = Guid.NewGuid(), Name = "Other Household" };

        context.AddRange(user, otherUser, first, second, unrelated);
        context.UserHouseholds.AddRange(
            new UserHousehold { Id = Guid.NewGuid(), UserId = user.Id, HouseholdId = second.Id },
            new UserHousehold { Id = Guid.NewGuid(), UserId = user.Id, HouseholdId = first.Id },
            new UserHousehold { Id = Guid.NewGuid(), UserId = otherUser.Id, HouseholdId = unrelated.Id });
        await context.SaveChangesAsync();

        var repository = new UserRepository(context);

        var households = await repository.GetHouseholdsAsync(user.Id);

        Assert.Equal(new[] { "Alpha Household", "Zeta Household" }, households.Select(household => household.Name));
    }

    [Fact]
    public async Task GetHouseholdsAsync_ReturnsEmptyForAUserWithNoMembership()
    {
        await using var context = CreateContext(nameof(GetHouseholdsAsync_ReturnsEmptyForAUserWithNoMembership));
        var user = new User { Id = Guid.NewGuid(), Name = "Alex", Email = "alex@example.com" };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var repository = new UserRepository(context);

        var households = await repository.GetHouseholdsAsync(user.Id);

        Assert.Empty(households);
    }

    [Fact]
    public async Task SaveChangesAsync_PersistsATrackedNameChange()
    {
        const string databaseName = nameof(SaveChangesAsync_PersistsATrackedNameChange);
        var userId = Guid.NewGuid();

        await using (var seedContext = CreateContext(databaseName))
        {
            seedContext.Users.Add(new User { Id = userId, Name = "Old Name", Email = "alex@example.com" });
            await seedContext.SaveChangesAsync();
        }

        await using (var updateContext = CreateContext(databaseName))
        {
            var repository = new UserRepository(updateContext);
            var user = await repository.GetByIdAsync(userId);
            Assert.NotNull(user);

            user!.Name = "New Name";
            await repository.SaveChangesAsync();
        }

        await using (var verifyContext = CreateContext(databaseName))
        {
            var persisted = await verifyContext.Users.SingleAsync(user => user.Id == userId);
            Assert.Equal("New Name", persisted.Name);
        }
    }

    [Fact]
    public void Constructor_RejectsANullContext()
    {
        Assert.Throws<ArgumentNullException>(() => new UserRepository(null!));
    }

    private static HouseholdFinancesDbContext CreateContext(string databaseName) =>
        new(new DbContextOptionsBuilder<HouseholdFinancesDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options);
}
