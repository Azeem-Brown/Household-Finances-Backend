using HouseholdFinances.Domain.Abstractions;
using HouseholdFinances.Domain.Entities;
using HouseholdFinances.Domain.Errors;
using HouseholdFinances.Domain.Models;
using HouseholdFinances.Infrastructure.Persistence;
using HouseholdFinances.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace HouseholdFinances.Tests.Services;

/// <summary>
/// Verifies the Goal service's create/list/update behaviour, the household scoping and authorization
/// rules, the validation rules, the current-user resolution, the money rounding, the contribution
/// total defaults, and the nullable interval round-trip. A goal carries its household key directly,
/// so scoping is by household rather than by the owning user's membership. The service uses the data
/// context directly, so it is exercised against the in-memory provider.
/// </summary>
public class GoalServiceTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid MemberUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid OtherUserId = Guid.Parse("99999999-9999-9999-9999-999999999999");
    private static readonly Guid HouseholdId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid OtherHouseholdId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly DateTime Start = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime End = new(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task CreateAsync_AttributesTheGoalToTheCurrentUserAndHouseholdAndReturnsItInTheList()
    {
        await using var context = CreateContext(nameof(CreateAsync_AttributesTheGoalToTheCurrentUserAndHouseholdAndReturnsItInTheList));
        SeedMembership(context, HouseholdId, UserId);
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var detail = await service.CreateAsync(
            HouseholdId,
            new GoalInput("  Emergency Fund  ", 10000.00, Start, End, true, Interval.Monthly, 0));

        Assert.NotEqual(Guid.Empty, detail.Id);
        Assert.Equal("Emergency Fund", detail.Name);
        Assert.Equal(10000.00, detail.Value);
        Assert.Equal(Start, detail.StartDate);
        Assert.Equal(End, detail.EndDate);
        Assert.True(detail.Recurring);
        Assert.Equal(Interval.Monthly, detail.Interval);
        Assert.Equal(HouseholdId, detail.HouseholdId);
        Assert.Equal(UserId, detail.UserId);

        var goal = Assert.Single(await service.ListAsync(HouseholdId));
        Assert.Equal(detail.Id, goal.Id);
    }

    [Fact]
    public async Task CreateAsync_DefaultsTheTotalToZeroEvenWhenTheInputSuppliesOne()
    {
        await using var context = CreateContext(nameof(CreateAsync_DefaultsTheTotalToZeroEvenWhenTheInputSuppliesOne));
        SeedMembership(context, HouseholdId, UserId);
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var detail = await service.CreateAsync(
            HouseholdId,
            new GoalInput("Emergency Fund", 10000.00, Start, End, true, Interval.Monthly, 1234.56));

        // A goal starts with no contributions; the total is changed only through update.
        Assert.Equal(GoalService.InitialTotal, detail.Total);
        Assert.Equal(0d, detail.Total);
    }

    [Fact]
    public async Task CreateAsync_RoundsTheValueToTwoDecimalPlacesHalfUp()
    {
        await using var context = CreateContext(nameof(CreateAsync_RoundsTheValueToTwoDecimalPlacesHalfUp));
        SeedMembership(context, HouseholdId, UserId);
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var detail = await service.CreateAsync(
            HouseholdId,
            new GoalInput("Emergency Fund", 10000.126, Start, End, false, null, 0));

        Assert.Equal(10000.13, detail.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateAsync_RejectsABlankName(string? name)
    {
        await using var context = CreateContext(Guid.NewGuid().ToString());
        SeedMembership(context, HouseholdId, UserId);
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var exception = await Assert.ThrowsAsync<HouseholdFinancesException>(
            () => service.CreateAsync(HouseholdId, new GoalInput(name, 100.00, Start, End, false, null, 0)));

        Assert.Equal(ErrorCode.InvalidInput, exception.ErrorCode);
        Assert.Equal(GoalService.NameIdentifier, exception.Identifier);
        Assert.Equal("InvalidInput Name", exception.Message);
    }

    [Fact]
    public async Task CreateAsync_RejectsANegativeValue()
    {
        await using var context = CreateContext(nameof(CreateAsync_RejectsANegativeValue));
        SeedMembership(context, HouseholdId, UserId);
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var exception = await Assert.ThrowsAsync<HouseholdFinancesException>(
            () => service.CreateAsync(HouseholdId, new GoalInput("Emergency Fund", -0.01, Start, End, false, null, 0)));

        Assert.Equal(ErrorCode.InvalidInput, exception.ErrorCode);
        Assert.Equal(GoalService.ValueIdentifier, exception.Identifier);
    }

    [Fact]
    public async Task CreateAsync_RejectsAnEndDateBeforeTheStartDate()
    {
        await using var context = CreateContext(nameof(CreateAsync_RejectsAnEndDateBeforeTheStartDate));
        SeedMembership(context, HouseholdId, UserId);
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var exception = await Assert.ThrowsAsync<HouseholdFinancesException>(
            () => service.CreateAsync(HouseholdId, new GoalInput("Emergency Fund", 100.00, End, Start, false, null, 0)));

        Assert.Equal(ErrorCode.InvalidInput, exception.ErrorCode);
        Assert.Equal(GoalService.EndDateIdentifier, exception.Identifier);
    }

    [Fact]
    public async Task CreateAsync_RejectsARecurringGoalWithoutAnInterval()
    {
        await using var context = CreateContext(nameof(CreateAsync_RejectsARecurringGoalWithoutAnInterval));
        SeedMembership(context, HouseholdId, UserId);
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var exception = await Assert.ThrowsAsync<HouseholdFinancesException>(
            () => service.CreateAsync(HouseholdId, new GoalInput("Emergency Fund", 100.00, Start, End, true, null, 0)));

        Assert.Equal(ErrorCode.InvalidInput, exception.ErrorCode);
        Assert.Equal(GoalService.IntervalIdentifier, exception.Identifier);
    }

    [Fact]
    public async Task CreateAsync_AllowsANonRecurringGoalWithoutAnIntervalAndStoresNull()
    {
        await using var context = CreateContext(nameof(CreateAsync_AllowsANonRecurringGoalWithoutAnIntervalAndStoresNull));
        SeedMembership(context, HouseholdId, UserId);
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var detail = await service.CreateAsync(
            HouseholdId,
            new GoalInput("One-off deposit", 500.00, Start, End, false, null, 0));

        Assert.False(detail.Recurring);
        Assert.Null(detail.Interval);

        // Round-trip: the stored one-off goal comes back with a null interval, not the zero-valued
        // Daily, so it is distinguishable from a daily goal.
        var goal = Assert.Single(await service.ListAsync(HouseholdId));
        Assert.Null(goal.Interval);
    }

    [Fact]
    public async Task CreateAsync_RecurringGoalRoundTripsItsInterval()
    {
        await using var context = CreateContext(nameof(CreateAsync_RecurringGoalRoundTripsItsInterval));
        SeedMembership(context, HouseholdId, UserId);
        await context.SaveChangesAsync();
        var service = CreateService(context);

        await service.CreateAsync(
            HouseholdId,
            new GoalInput("Emergency Fund", 10000.00, Start, End, true, Interval.Monthly, 0));

        var goal = Assert.Single(await service.ListAsync(HouseholdId));
        Assert.True(goal.Recurring);
        Assert.Equal(Interval.Monthly, goal.Interval);
    }

    [Fact]
    public async Task CreateAsync_ThrowsNotFoundForANonMember()
    {
        await using var context = CreateContext(nameof(CreateAsync_ThrowsNotFoundForANonMember));
        SeedMembership(context, HouseholdId, OtherUserId);
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var exception = await Assert.ThrowsAsync<HouseholdFinancesException>(
            () => service.CreateAsync(HouseholdId, new GoalInput("Emergency Fund", 100.00, Start, End, false, null, 0)));

        Assert.Equal(ErrorCode.NotFound, exception.ErrorCode);
        Assert.Equal(HouseholdId, exception.Identifier);
        Assert.Empty(context.Goals);
    }

    [Fact]
    public async Task CreateAsync_ThrowsUnauthorizedWhenTheCurrentUserCannotBeResolved()
    {
        await using var context = CreateContext(nameof(CreateAsync_ThrowsUnauthorizedWhenTheCurrentUserCannotBeResolved));
        var currentUser = new StubCurrentUserService { IsAuthenticated = false, UserIdentifier = null };
        var service = new GoalService(context, currentUser);

        var exception = await Assert.ThrowsAsync<HouseholdFinancesException>(
            () => service.CreateAsync(HouseholdId, new GoalInput("Emergency Fund", 100.00, Start, End, false, null, 0)));

        Assert.Equal(ErrorCode.Unauthorized, exception.ErrorCode);
        Assert.Equal(GoalService.CurrentUserIdentifier, exception.Identifier);
    }

    [Fact]
    public async Task ListAsync_ReturnsEveryGoalOfTheHouseholdAndExcludesOtherHouseholds()
    {
        await using var context = CreateContext(nameof(ListAsync_ReturnsEveryGoalOfTheHouseholdAndExcludesOtherHouseholds));
        SeedMembership(context, HouseholdId, UserId);
        SeedMembership(context, HouseholdId, MemberUserId);
        SeedMembership(context, OtherHouseholdId, OtherUserId);
        context.Goals.AddRange(
            new Goal { Id = Guid.NewGuid(), Name = "Holiday", Value = 3000.00, StartDate = End, EndDate = End, HouseholdId = HouseholdId, UserId = UserId },
            new Goal { Id = Guid.NewGuid(), Name = "Car", Value = 8000.00, StartDate = Start, EndDate = Start, HouseholdId = HouseholdId, UserId = MemberUserId },
            new Goal { Id = Guid.NewGuid(), Name = "Other household", Value = 9999.00, StartDate = Start, EndDate = Start, HouseholdId = OtherHouseholdId, UserId = OtherUserId });
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var goals = await service.ListAsync(HouseholdId);

        Assert.Equal(2, goals.Count);
        // Ordered by start date, then name: Car (Start) precedes Holiday (End).
        Assert.Equal(new[] { "Car", "Holiday" }, goals.Select(goal => goal.Name));
        // A goal created by another member of the same household is still visible.
        Assert.Contains(goals, goal => goal.UserId == MemberUserId);
    }

    [Fact]
    public async Task ListAsync_ThrowsNotFoundForANonMember()
    {
        await using var context = CreateContext(nameof(ListAsync_ThrowsNotFoundForANonMember));
        SeedMembership(context, HouseholdId, OtherUserId);
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var exception = await Assert.ThrowsAsync<HouseholdFinancesException>(
            () => service.ListAsync(HouseholdId));

        Assert.Equal(ErrorCode.NotFound, exception.ErrorCode);
        Assert.Equal(HouseholdId, exception.Identifier);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesTheFieldsIncludingTotalAndPreservesTheOwnerAndHousehold()
    {
        await using var context = CreateContext(nameof(UpdateAsync_UpdatesTheFieldsIncludingTotalAndPreservesTheOwnerAndHousehold));
        SeedMembership(context, HouseholdId, UserId);
        SeedMembership(context, HouseholdId, MemberUserId);
        var goalId = Guid.NewGuid();
        context.Goals.Add(new Goal
        {
            Id = goalId,
            Name = "Old",
            Value = 100.00,
            StartDate = Start,
            EndDate = End,
            Recurring = false,
            Interval = null,
            HouseholdId = HouseholdId,
            UserId = MemberUserId,
            Total = 0
        });
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var detail = await service.UpdateAsync(
            HouseholdId,
            goalId,
            new GoalInput("Updated", 12000.50, Start, End, true, Interval.Monthly, 2500.00));

        Assert.Equal(goalId, detail.Id);
        Assert.Equal("Updated", detail.Name);
        Assert.Equal(12000.50, detail.Value);
        Assert.True(detail.Recurring);
        Assert.Equal(Interval.Monthly, detail.Interval);
        Assert.Equal(2500.00, detail.Total);
        // Attribution and household are immutable: the goal stays in its household and owned by the
        // member who created it.
        Assert.Equal(MemberUserId, detail.UserId);
        Assert.Equal(HouseholdId, detail.HouseholdId);

        var goal = Assert.Single(await service.ListAsync(HouseholdId));
        Assert.Equal("Updated", goal.Name);
        Assert.Equal(2500.00, goal.Total);
    }

    [Fact]
    public async Task UpdateAsync_RecurringGoalChangedToOneOffStoresNull()
    {
        await using var context = CreateContext(nameof(UpdateAsync_RecurringGoalChangedToOneOffStoresNull));
        SeedMembership(context, HouseholdId, UserId);
        var goalId = Guid.NewGuid();
        context.Goals.Add(new Goal
        {
            Id = goalId,
            Name = "Emergency Fund",
            Value = 10000.00,
            StartDate = Start,
            EndDate = End,
            Recurring = true,
            Interval = Interval.Monthly,
            HouseholdId = HouseholdId,
            UserId = UserId,
            Total = 500.00
        });
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var detail = await service.UpdateAsync(
            HouseholdId,
            goalId,
            new GoalInput("One-off deposit", 500.00, Start, End, false, null, 500.00));

        Assert.False(detail.Recurring);
        Assert.Null(detail.Interval);

        var goal = Assert.Single(await service.ListAsync(HouseholdId));
        Assert.Null(goal.Interval);
    }

    [Fact]
    public async Task UpdateAsync_ThrowsNotFoundForANonMember()
    {
        await using var context = CreateContext(nameof(UpdateAsync_ThrowsNotFoundForANonMember));
        SeedMembership(context, HouseholdId, OtherUserId);
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var exception = await Assert.ThrowsAsync<HouseholdFinancesException>(
            () => service.UpdateAsync(
                HouseholdId,
                Guid.NewGuid(),
                new GoalInput("Updated", 100.00, Start, End, false, null, 0)));

        Assert.Equal(ErrorCode.NotFound, exception.ErrorCode);
        Assert.Equal(HouseholdId, exception.Identifier);
    }

    [Fact]
    public async Task UpdateAsync_ThrowsNotFoundForAGoalOutsideTheHousehold()
    {
        await using var context = CreateContext(nameof(UpdateAsync_ThrowsNotFoundForAGoalOutsideTheHousehold));
        SeedMembership(context, HouseholdId, UserId);
        SeedMembership(context, OtherHouseholdId, OtherUserId);
        var foreignGoalId = Guid.NewGuid();
        context.Goals.Add(new Goal
        {
            Id = foreignGoalId,
            Name = "Other household",
            Value = 100.00,
            StartDate = Start,
            EndDate = End,
            HouseholdId = OtherHouseholdId,
            UserId = OtherUserId
        });
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var exception = await Assert.ThrowsAsync<HouseholdFinancesException>(
            () => service.UpdateAsync(
                HouseholdId,
                foreignGoalId,
                new GoalInput("Updated", 100.00, Start, End, false, null, 0)));

        Assert.Equal(ErrorCode.NotFound, exception.ErrorCode);
        Assert.Equal(foreignGoalId, exception.Identifier);
    }

    [Fact]
    public void Constructor_RejectsNullDependencies()
    {
        using var context = CreateContext(nameof(Constructor_RejectsNullDependencies));

        Assert.Throws<ArgumentNullException>(() => new GoalService(null!, new StubCurrentUserService()));
        Assert.Throws<ArgumentNullException>(() => new GoalService(context, null!));
    }

    private static HouseholdFinancesDbContext CreateContext(string databaseName) =>
        new(new DbContextOptionsBuilder<HouseholdFinancesDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options);

    private static GoalService CreateService(HouseholdFinancesDbContext context, Guid? userId = null) =>
        new(context, new StubCurrentUserService { UserIdentifier = (userId ?? UserId).ToString() });

    private static void SeedMembership(HouseholdFinancesDbContext context, Guid householdId, Guid userId) =>
        context.UserHouseholds.Add(new UserHousehold
        {
            Id = Guid.NewGuid(),
            HouseholdId = householdId,
            UserId = userId
        });

    private sealed class StubCurrentUserService : ICurrentUserService
    {
        public bool IsAuthenticated { get; set; } = true;

        public string? UserIdentifier { get; set; } = UserId.ToString();
    }
}
