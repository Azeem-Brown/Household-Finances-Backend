using HouseholdFinances.Domain.Abstractions;
using HouseholdFinances.Domain.Entities;
using HouseholdFinances.Domain.Errors;
using HouseholdFinances.Domain.Models;
using HouseholdFinances.Infrastructure.Persistence;
using HouseholdFinances.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace HouseholdFinances.Tests.Services;

/// <summary>
/// Verifies the Income service's create/list/update/delete behaviour, the household scoping and
/// authorization rules, the validation rules, the current-user resolution, and the money rounding.
/// The service uses the data context directly, so it is exercised against the in-memory provider.
/// </summary>
public class IncomeServiceTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid MemberUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid OtherUserId = Guid.Parse("99999999-9999-9999-9999-999999999999");
    private static readonly Guid HouseholdId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid OtherHouseholdId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly DateTime Start = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime End = new(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task CreateAsync_AttributesTheEntryToTheCurrentUserAndReturnsItInTheHouseholdList()
    {
        await using var context = CreateContext(nameof(CreateAsync_AttributesTheEntryToTheCurrentUserAndReturnsItInTheHouseholdList));
        SeedMembership(context, HouseholdId, UserId);
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var detail = await service.CreateAsync(
            HouseholdId,
            new IncomeInput("  Salary  ", 2600.00, Start, End, true, Interval.BiWeekly));

        Assert.NotEqual(Guid.Empty, detail.Id);
        Assert.Equal("Salary", detail.Name);
        Assert.Equal(2600.00, detail.Value);
        Assert.Equal(Start, detail.StartDate);
        Assert.Equal(End, detail.EndDate);
        Assert.True(detail.Recurring);
        Assert.Equal(Interval.BiWeekly, detail.Interval);
        Assert.Equal(UserId, detail.UserId);

        var entry = Assert.Single(await service.ListAsync(HouseholdId));
        Assert.Equal(detail.Id, entry.Id);
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
            new IncomeInput("Salary", 2600.126, Start, End, false, null));

        Assert.Equal(2600.13, detail.Value);
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
            () => service.CreateAsync(HouseholdId, new IncomeInput(name, 100.00, Start, End, false, null)));

        Assert.Equal(ErrorCode.InvalidInput, exception.ErrorCode);
        Assert.Equal(IncomeService.NameIdentifier, exception.Identifier);
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
            () => service.CreateAsync(HouseholdId, new IncomeInput("Salary", -0.01, Start, End, false, null)));

        Assert.Equal(ErrorCode.InvalidInput, exception.ErrorCode);
        Assert.Equal(IncomeService.ValueIdentifier, exception.Identifier);
    }

    [Fact]
    public async Task CreateAsync_RejectsAnEndDateBeforeTheStartDate()
    {
        await using var context = CreateContext(nameof(CreateAsync_RejectsAnEndDateBeforeTheStartDate));
        SeedMembership(context, HouseholdId, UserId);
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var exception = await Assert.ThrowsAsync<HouseholdFinancesException>(
            () => service.CreateAsync(HouseholdId, new IncomeInput("Salary", 100.00, End, Start, false, null)));

        Assert.Equal(ErrorCode.InvalidInput, exception.ErrorCode);
        Assert.Equal(IncomeService.EndDateIdentifier, exception.Identifier);
    }

    [Fact]
    public async Task CreateAsync_RejectsARecurringEntryWithoutAnInterval()
    {
        await using var context = CreateContext(nameof(CreateAsync_RejectsARecurringEntryWithoutAnInterval));
        SeedMembership(context, HouseholdId, UserId);
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var exception = await Assert.ThrowsAsync<HouseholdFinancesException>(
            () => service.CreateAsync(HouseholdId, new IncomeInput("Salary", 100.00, Start, End, true, null)));

        Assert.Equal(ErrorCode.InvalidInput, exception.ErrorCode);
        Assert.Equal(IncomeService.IntervalIdentifier, exception.Identifier);
    }

    [Fact]
    public async Task CreateAsync_AllowsANonRecurringEntryWithoutAnIntervalAndStoresNull()
    {
        await using var context = CreateContext(nameof(CreateAsync_AllowsANonRecurringEntryWithoutAnIntervalAndStoresNull));
        SeedMembership(context, HouseholdId, UserId);
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var detail = await service.CreateAsync(
            HouseholdId,
            new IncomeInput("Bonus", 500.00, Start, End, false, null));

        Assert.False(detail.Recurring);
        Assert.Null(detail.Interval);

        // Round-trip: the stored one-off entry comes back with a null interval, not the zero-valued
        // Daily, so it is distinguishable from a daily entry.
        var entry = Assert.Single(await service.ListAsync(HouseholdId));
        Assert.Null(entry.Interval);
    }

    [Fact]
    public async Task CreateAsync_RecurringEntryRoundTripsItsInterval()
    {
        await using var context = CreateContext(nameof(CreateAsync_RecurringEntryRoundTripsItsInterval));
        SeedMembership(context, HouseholdId, UserId);
        await context.SaveChangesAsync();
        var service = CreateService(context);

        await service.CreateAsync(
            HouseholdId,
            new IncomeInput("Salary", 2600.00, Start, End, true, Interval.BiWeekly));

        var entry = Assert.Single(await service.ListAsync(HouseholdId));
        Assert.True(entry.Recurring);
        Assert.Equal(Interval.BiWeekly, entry.Interval);
    }

    [Fact]
    public async Task UpdateAsync_RecurringEntryChangedToOneOffStoresNull()
    {
        await using var context = CreateContext(nameof(UpdateAsync_RecurringEntryChangedToOneOffStoresNull));
        SeedMembership(context, HouseholdId, UserId);
        var incomeId = Guid.NewGuid();
        context.Incomes.Add(new Income
        {
            Id = incomeId,
            Name = "Salary",
            Value = 2600.00,
            StartDate = Start,
            EndDate = End,
            Recurring = true,
            Interval = Interval.BiWeekly,
            UserId = UserId
        });
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var detail = await service.UpdateAsync(
            HouseholdId,
            incomeId,
            new IncomeInput("Bonus", 500.00, Start, End, false, null));

        Assert.False(detail.Recurring);
        Assert.Null(detail.Interval);

        var entry = Assert.Single(await service.ListAsync(HouseholdId));
        Assert.Null(entry.Interval);
    }

    [Fact]
    public async Task CreateAsync_ThrowsNotFoundForANonMember()
    {
        await using var context = CreateContext(nameof(CreateAsync_ThrowsNotFoundForANonMember));
        SeedMembership(context, HouseholdId, OtherUserId);
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var exception = await Assert.ThrowsAsync<HouseholdFinancesException>(
            () => service.CreateAsync(HouseholdId, new IncomeInput("Salary", 100.00, Start, End, false, null)));

        Assert.Equal(ErrorCode.NotFound, exception.ErrorCode);
        Assert.Equal(HouseholdId, exception.Identifier);
        Assert.Empty(context.Incomes);
    }

    [Fact]
    public async Task CreateAsync_ThrowsUnauthorizedWhenTheCurrentUserCannotBeResolved()
    {
        await using var context = CreateContext(nameof(CreateAsync_ThrowsUnauthorizedWhenTheCurrentUserCannotBeResolved));
        var currentUser = new StubCurrentUserService { IsAuthenticated = false, UserIdentifier = null };
        var service = new IncomeService(context, currentUser);

        var exception = await Assert.ThrowsAsync<HouseholdFinancesException>(
            () => service.CreateAsync(HouseholdId, new IncomeInput("Salary", 100.00, Start, End, false, null)));

        Assert.Equal(ErrorCode.Unauthorized, exception.ErrorCode);
        Assert.Equal(IncomeService.CurrentUserIdentifier, exception.Identifier);
    }

    [Fact]
    public async Task ListAsync_ReturnsEveryMembersEntriesAndExcludesOtherHouseholds()
    {
        await using var context = CreateContext(nameof(ListAsync_ReturnsEveryMembersEntriesAndExcludesOtherHouseholds));
        SeedMembership(context, HouseholdId, UserId);
        SeedMembership(context, HouseholdId, MemberUserId);
        SeedMembership(context, OtherHouseholdId, OtherUserId);
        context.Incomes.AddRange(
            new Income { Id = Guid.NewGuid(), Name = "Salary", Value = 2000.00, StartDate = End, EndDate = End, UserId = UserId },
            new Income { Id = Guid.NewGuid(), Name = "Bonus", Value = 250.00, StartDate = Start, EndDate = Start, UserId = MemberUserId },
            new Income { Id = Guid.NewGuid(), Name = "Other", Value = 9999.00, StartDate = Start, EndDate = Start, UserId = OtherUserId });
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var entries = await service.ListAsync(HouseholdId);

        Assert.Equal(2, entries.Count);
        // Ordered by start date, then name: Bonus (Start) precedes Salary (End).
        Assert.Equal(new[] { "Bonus", "Salary" }, entries.Select(entry => entry.Name));
        Assert.Contains(entries, entry => entry.UserId == MemberUserId);
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
    public async Task UpdateAsync_UpdatesTheFieldsAndPreservesTheOwner()
    {
        await using var context = CreateContext(nameof(UpdateAsync_UpdatesTheFieldsAndPreservesTheOwner));
        SeedMembership(context, HouseholdId, UserId);
        SeedMembership(context, HouseholdId, MemberUserId);
        var incomeId = Guid.NewGuid();
        context.Incomes.Add(new Income
        {
            Id = incomeId,
            Name = "Old",
            Value = 100.00,
            StartDate = Start,
            EndDate = End,
            Recurring = false,
            Interval = Interval.Daily,
            UserId = MemberUserId
        });
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var detail = await service.UpdateAsync(
            HouseholdId,
            incomeId,
            new IncomeInput("Updated", 3200.50, Start, End, true, Interval.Monthly));

        Assert.Equal(incomeId, detail.Id);
        Assert.Equal("Updated", detail.Name);
        Assert.Equal(3200.50, detail.Value);
        Assert.True(detail.Recurring);
        Assert.Equal(Interval.Monthly, detail.Interval);
        // Attribution is immutable: the entry stays owned by the member who created it.
        Assert.Equal(MemberUserId, detail.UserId);

        var entry = Assert.Single(await service.ListAsync(HouseholdId));
        Assert.Equal("Updated", entry.Name);
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
                new IncomeInput("Updated", 100.00, Start, End, false, null)));

        Assert.Equal(ErrorCode.NotFound, exception.ErrorCode);
        Assert.Equal(HouseholdId, exception.Identifier);
    }

    [Fact]
    public async Task UpdateAsync_ThrowsNotFoundForAnEntryOutsideTheHousehold()
    {
        await using var context = CreateContext(nameof(UpdateAsync_ThrowsNotFoundForAnEntryOutsideTheHousehold));
        SeedMembership(context, HouseholdId, UserId);
        SeedMembership(context, OtherHouseholdId, OtherUserId);
        var foreignIncomeId = Guid.NewGuid();
        context.Incomes.Add(new Income
        {
            Id = foreignIncomeId,
            Name = "Other",
            Value = 100.00,
            StartDate = Start,
            EndDate = End,
            UserId = OtherUserId
        });
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var exception = await Assert.ThrowsAsync<HouseholdFinancesException>(
            () => service.UpdateAsync(
                HouseholdId,
                foreignIncomeId,
                new IncomeInput("Updated", 100.00, Start, End, false, null)));

        Assert.Equal(ErrorCode.NotFound, exception.ErrorCode);
        Assert.Equal(foreignIncomeId, exception.Identifier);
    }

    [Fact]
    public async Task DeleteAsync_RemovesTheEntryAndTheListReflectsTheChange()
    {
        await using var context = CreateContext(nameof(DeleteAsync_RemovesTheEntryAndTheListReflectsTheChange));
        SeedMembership(context, HouseholdId, UserId);
        SeedMembership(context, HouseholdId, MemberUserId);
        var incomeId = Guid.NewGuid();
        context.Incomes.Add(new Income
        {
            Id = incomeId,
            Name = "Salary",
            Value = 2000.00,
            StartDate = Start,
            EndDate = End,
            UserId = MemberUserId
        });
        await context.SaveChangesAsync();
        var service = CreateService(context);

        await service.DeleteAsync(HouseholdId, incomeId);

        Assert.Empty(await service.ListAsync(HouseholdId));
    }

    [Fact]
    public async Task DeleteAsync_ThrowsNotFoundForANonMember()
    {
        await using var context = CreateContext(nameof(DeleteAsync_ThrowsNotFoundForANonMember));
        SeedMembership(context, HouseholdId, OtherUserId);
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var exception = await Assert.ThrowsAsync<HouseholdFinancesException>(
            () => service.DeleteAsync(HouseholdId, Guid.NewGuid()));

        Assert.Equal(ErrorCode.NotFound, exception.ErrorCode);
        Assert.Equal(HouseholdId, exception.Identifier);
    }

    [Fact]
    public async Task DeleteAsync_ThrowsNotFoundForAnEntryOutsideTheHousehold()
    {
        await using var context = CreateContext(nameof(DeleteAsync_ThrowsNotFoundForAnEntryOutsideTheHousehold));
        SeedMembership(context, HouseholdId, UserId);
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var exception = await Assert.ThrowsAsync<HouseholdFinancesException>(
            () => service.DeleteAsync(HouseholdId, Guid.NewGuid()));

        Assert.Equal(ErrorCode.NotFound, exception.ErrorCode);
    }

    [Fact]
    public void Constructor_RejectsNullDependencies()
    {
        using var context = CreateContext(nameof(Constructor_RejectsNullDependencies));

        Assert.Throws<ArgumentNullException>(() => new IncomeService(null!, new StubCurrentUserService()));
        Assert.Throws<ArgumentNullException>(() => new IncomeService(context, null!));
    }

    private static HouseholdFinancesDbContext CreateContext(string databaseName) =>
        new(new DbContextOptionsBuilder<HouseholdFinancesDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options);

    private static IncomeService CreateService(HouseholdFinancesDbContext context, Guid? userId = null) =>
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
