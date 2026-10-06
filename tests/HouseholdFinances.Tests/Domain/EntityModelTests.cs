using HouseholdFinances.Domain.Entities;

namespace HouseholdFinances.Tests.Domain;

public class EntityModelTests
{
    [Fact]
    public void Users_PasswordCanBeNull()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = "Alex Doe",
            Email = "alex@example.com",
            Password = null
        };

        Assert.NotEqual(Guid.Empty, user.Id);
        Assert.Equal("Alex Doe", user.Name);
        Assert.Equal("alex@example.com", user.Email);
        Assert.Null(user.Password);
    }

    [Fact]
    public void Items_DescriptionCanBeNull()
    {
        var item = new Item
        {
            Id = Guid.NewGuid(),
            Name = "Groceries",
            Description = null,
            Price = 42.50,
            HouseholdId = Guid.NewGuid()
        };

        Assert.NotEqual(Guid.Empty, item.Id);
        Assert.Equal("Groceries", item.Name);
        Assert.Null(item.Description);
        Assert.Equal(42.50, item.Price);
    }

    [Fact]
    public void Entities_ConstructWithRepresentativeValues()
    {
        var householdId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var start = new DateTime(2026, 1, 1);
        var end = new DateTime(2026, 12, 31);

        var household = new Household
        {
            Id = householdId,
            Name = "Brown Household",
            Incomes = 5200.00,
            Payments = 3100.50
        };

        var income = new Income
        {
            Id = Guid.NewGuid(),
            Name = "Salary",
            Value = 2600.00,
            StartDate = start,
            EndDate = end,
            Recurring = true,
            Interval = Interval.BiWeekly,
            UserId = userId
        };

        var bill = new Bill
        {
            Id = Guid.NewGuid(),
            Name = "Rent",
            Value = 1500.00,
            StartDate = start,
            EndDate = end,
            Recurring = true,
            Interval = Interval.Monthly,
            HouseholdId = householdId,
            UserId = userId
        };

        var item = new Item
        {
            Id = Guid.NewGuid(),
            Name = "Groceries",
            Description = "Weekly shopping",
            Price = 42.50,
            HouseholdId = householdId
        };

        var membership = new UserHousehold
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            HouseholdId = householdId
        };

        var goal = new Goal
        {
            Id = Guid.NewGuid(),
            Name = "Emergency Fund",
            Value = 10000.00,
            StartDate = start,
            EndDate = end,
            Recurring = true,
            Interval = Interval.Monthly,
            HouseholdId = householdId,
            UserId = userId,
            Total = 1200.00
        };

        Assert.Equal(householdId, household.Id);
        Assert.Equal("Brown Household", household.Name);
        Assert.Equal(5200.00, household.Incomes);
        Assert.Equal(3100.50, household.Payments);

        Assert.Equal(userId, income.UserId);
        Assert.Equal(Interval.BiWeekly, income.Interval);
        Assert.Equal(start, income.StartDate);
        Assert.Equal(end, income.EndDate);

        Assert.Equal(householdId, bill.HouseholdId);
        Assert.Equal(userId, bill.UserId);
        Assert.Equal(Interval.Monthly, bill.Interval);

        Assert.Equal("Weekly shopping", item.Description);
        Assert.Equal(householdId, item.HouseholdId);

        Assert.Equal(userId, membership.UserId);
        Assert.Equal(householdId, membership.HouseholdId);

        Assert.Equal(householdId, goal.HouseholdId);
        Assert.Equal(userId, goal.UserId);
        Assert.Equal(Interval.Monthly, goal.Interval);
        Assert.Equal(10000.00, goal.Value);
        Assert.Equal(1200.00, goal.Total);
    }

    [Fact]
    public void Interval_ContainsExactlyTheApprovedValues()
    {
        var expected = new[]
        {
            Interval.Daily,
            Interval.Weekly,
            Interval.BiWeekly,
            Interval.Monthly,
            Interval.Quarterly,
            Interval.Yearly
        };

        var actual = Enum.GetValues<Interval>();

        Assert.Equal(6, actual.Length);
        Assert.Equal(expected, actual);
    }
}
