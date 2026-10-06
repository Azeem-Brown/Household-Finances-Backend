using HouseholdFinances.Domain.Entities;
using HouseholdFinances.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HouseholdFinances.Tests.Persistence;

public class HouseholdFinancesDbContextTests
{
    // Credential-free placeholder; no database connection is opened by these tests.
    private const string TestConnectionString = "Server=localhost;Database=household_finances_tests;";

    private static IConfiguration BuildTestConfiguration(string? connectionString = TestConnectionString) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = connectionString
            })
            .Build();

    private static HouseholdFinancesDbContext CreateContext(IConfiguration configuration, out ServiceProvider provider)
    {
        var services = new ServiceCollection();
        services.AddHouseholdFinancesDbContext(configuration);

        provider = services.BuildServiceProvider();
        var scope = provider.CreateScope();

        return scope.ServiceProvider.GetRequiredService<HouseholdFinancesDbContext>();
    }

    [Fact]
    public void Context_InstantiatesAgainstTestConfiguration_AndExposesAllSevenEntities()
    {
        var context = CreateContext(BuildTestConfiguration(), out var provider);
        using (provider)
        {
            var model = context.Model;

            Assert.NotNull(model.FindEntityType(typeof(Household)));
            Assert.NotNull(model.FindEntityType(typeof(User)));
            Assert.NotNull(model.FindEntityType(typeof(UserHousehold)));
            Assert.NotNull(model.FindEntityType(typeof(Income)));
            Assert.NotNull(model.FindEntityType(typeof(Bill)));
            Assert.NotNull(model.FindEntityType(typeof(Item)));
            Assert.NotNull(model.FindEntityType(typeof(Goal)));
        }
    }

    [Fact]
    public void AddHouseholdFinancesDbContext_ThrowsWhenConnectionStringIsMissing()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        Assert.Throws<InvalidOperationException>(
            () => services.AddHouseholdFinancesDbContext(configuration));
    }

    [Fact]
    public void Model_StoresGuidKeysAsChar36_AndMoneyAsDouble()
    {
        var context = CreateContext(BuildTestConfiguration(), out var provider);
        using (provider)
        {
            var model = context.Model;

            var household = model.FindEntityType(typeof(Household));
            Assert.NotNull(household);
            Assert.Equal("char(36)", household.FindProperty(nameof(Household.Id))!.GetColumnType());
            Assert.Equal("double", household.FindProperty(nameof(Household.Incomes))!.GetColumnType());
            Assert.Equal("double", household.FindProperty(nameof(Household.Payments))!.GetColumnType());

            var item = model.FindEntityType(typeof(Item));
            Assert.NotNull(item);
            Assert.Equal("char(36)", item.FindProperty(nameof(Item.Id))!.GetColumnType());
            Assert.Equal("double", item.FindProperty(nameof(Item.Price))!.GetColumnType());

            var goal = model.FindEntityType(typeof(Goal));
            Assert.NotNull(goal);
            Assert.Equal("double", goal.FindProperty(nameof(Goal.Value))!.GetColumnType());
            Assert.Equal("double", goal.FindProperty(nameof(Goal.Total))!.GetColumnType());
        }
    }

    [Fact]
    public void Model_ConfiguresRequiredAndNullableFields()
    {
        var context = CreateContext(BuildTestConfiguration(), out var provider);
        using (provider)
        {
            var model = context.Model;

            var user = model.FindEntityType(typeof(User));
            Assert.NotNull(user);
            Assert.False(user.FindProperty(nameof(User.Name))!.IsNullable);
            Assert.False(user.FindProperty(nameof(User.Email))!.IsNullable);
            Assert.True(user.FindProperty(nameof(User.Password))!.IsNullable);

            var item = model.FindEntityType(typeof(Item));
            Assert.NotNull(item);
            Assert.False(item.FindProperty(nameof(Item.Name))!.IsNullable);
            Assert.True(item.FindProperty(nameof(Item.Description))!.IsNullable);
        }
    }

    [Fact]
    public void Model_ConfiguresHouseholdAndUserRelationships()
    {
        var context = CreateContext(BuildTestConfiguration(), out var provider);
        using (provider)
        {
            var model = context.Model;

            AssertForeignKey<Bill, Household>(model, nameof(Bill.HouseholdId));
            AssertForeignKey<Bill, User>(model, nameof(Bill.UserId));
            AssertForeignKey<Goal, Household>(model, nameof(Goal.HouseholdId));
            AssertForeignKey<Goal, User>(model, nameof(Goal.UserId));
            AssertForeignKey<Income, User>(model, nameof(Income.UserId));
            AssertForeignKey<Item, Household>(model, nameof(Item.HouseholdId));
            AssertForeignKey<UserHousehold, User>(model, nameof(UserHousehold.UserId));
            AssertForeignKey<UserHousehold, Household>(model, nameof(UserHousehold.HouseholdId));

            // Income is reached through the owning user's membership, not a household key.
            var income = model.FindEntityType(typeof(Income));
            Assert.NotNull(income);
            Assert.Null(income.FindProperty("HouseholdId"));
        }
    }

    [Fact]
    public void Model_EnforcesUniqueUserHouseholdMembership()
    {
        var context = CreateContext(BuildTestConfiguration(), out var provider);
        using (provider)
        {
            var membership = context.Model.FindEntityType(typeof(UserHousehold));
            Assert.NotNull(membership);

            // A user may belong to a household at most once, enforced at the database level.
            var index = membership.GetIndexes().SingleOrDefault(candidate =>
                candidate.Properties.Select(property => property.Name).SequenceEqual(
                    new[] { nameof(UserHousehold.UserId), nameof(UserHousehold.HouseholdId) }));

            Assert.NotNull(index);
            Assert.True(index!.IsUnique);
        }
    }

    private static void AssertForeignKey<TDependent, TPrincipal>(
        IModel model,
        string foreignKeyPropertyName)
    {
        var dependent = model.FindEntityType(typeof(TDependent));
        Assert.NotNull(dependent);

        var foreignKey = dependent.GetForeignKeys()
            .SingleOrDefault(fk => fk.Properties.Any(p => p.Name == foreignKeyPropertyName));

        Assert.NotNull(foreignKey);
        Assert.Equal(typeof(TPrincipal), foreignKey.PrincipalEntityType.ClrType);
    }
}
