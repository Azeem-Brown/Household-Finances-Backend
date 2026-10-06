using HouseholdFinances.Domain.Abstractions;
using HouseholdFinances.Domain.Entities;
using HouseholdFinances.Domain.Errors;
using HouseholdFinances.Domain.Models;
using HouseholdFinances.Infrastructure.Persistence;
using HouseholdFinances.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace HouseholdFinances.Tests.Services;

/// <summary>
/// Verifies the Bill service's create/list/update/delete behaviour, the household scoping and
/// authorization rules, the validation rules, the current-user resolution, and the money rounding.
/// A bill carries its household key directly, so scoping is by household rather than by the owning
/// user's membership. The service uses the data context directly, so it is exercised against the
/// in-memory provider.
/// </summary>
public class BillServiceTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid MemberUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid OtherUserId = Guid.Parse("99999999-9999-9999-9999-999999999999");
    private static readonly Guid HouseholdId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid OtherHouseholdId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly DateTime Start = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime End = new(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task CreateAsync_AttributesTheBillToTheCurrentUserAndHouseholdAndReturnsItInTheList()
    {
        await using var context = CreateContext(nameof(CreateAsync_AttributesTheBillToTheCurrentUserAndHouseholdAndReturnsItInTheList));
        SeedMembership(context, HouseholdId, UserId);
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var detail = await service.CreateAsync(
            HouseholdId,
            new BillInput("  Rent  ", 1200.00, Start, End, true, Interval.Monthly));

        Assert.NotEqual(Guid.Empty, detail.Id);
        Assert.Equal("Rent", detail.Name);
        Assert.Equal(1200.00, detail.Value);
        Assert.Equal(Start, detail.StartDate);
        Assert.Equal(End, detail.EndDate);
        Assert.True(detail.Recurring);
        Assert.Equal(Interval.Monthly, detail.Interval);
        Assert.Equal(HouseholdId, detail.HouseholdId);
        Assert.Equal(UserId, detail.UserId);

        var bill = Assert.Single(await service.ListAsync(HouseholdId));
        Assert.Equal(detail.Id, bill.Id);
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
            new BillInput("Rent", 1200.126, Start, End, false, null));

        Assert.Equal(1200.13, detail.Value);
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
            () => service.CreateAsync(HouseholdId, new BillInput(name, 100.00, Start, End, false, null)));

        Assert.Equal(ErrorCode.InvalidInput, exception.ErrorCode);
        Assert.Equal(BillService.NameIdentifier, exception.Identifier);
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
            () => service.CreateAsync(HouseholdId, new BillInput("Rent", -0.01, Start, End, false, null)));

        Assert.Equal(ErrorCode.InvalidInput, exception.ErrorCode);
        Assert.Equal(BillService.ValueIdentifier, exception.Identifier);
    }

    [Fact]
    public async Task CreateAsync_RejectsAnEndDateBeforeTheStartDate()
    {
        await using var context = CreateContext(nameof(CreateAsync_RejectsAnEndDateBeforeTheStartDate));
        SeedMembership(context, HouseholdId, UserId);
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var exception = await Assert.ThrowsAsync<HouseholdFinancesException>(
            () => service.CreateAsync(HouseholdId, new BillInput("Rent", 100.00, End, Start, false, null)));

        Assert.Equal(ErrorCode.InvalidInput, exception.ErrorCode);
        Assert.Equal(BillService.EndDateIdentifier, exception.Identifier);
    }

    [Fact]
    public async Task CreateAsync_RejectsARecurringBillWithoutAnInterval()
    {
        await using var context = CreateContext(nameof(CreateAsync_RejectsARecurringBillWithoutAnInterval));
        SeedMembership(context, HouseholdId, UserId);
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var exception = await Assert.ThrowsAsync<HouseholdFinancesException>(
            () => service.CreateAsync(HouseholdId, new BillInput("Rent", 100.00, Start, End, true, null)));

        Assert.Equal(ErrorCode.InvalidInput, exception.ErrorCode);
        Assert.Equal(BillService.IntervalIdentifier, exception.Identifier);
    }

    [Fact]
    public async Task CreateAsync_AllowsANonRecurringBillWithoutAnIntervalAndStoresDaily()
    {
        await using var context = CreateContext(nameof(CreateAsync_AllowsANonRecurringBillWithoutAnIntervalAndStoresDaily));
        SeedMembership(context, HouseholdId, UserId);
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var detail = await service.CreateAsync(
            HouseholdId,
            new BillInput("One-off repair", 500.00, Start, End, false, null));

        Assert.False(detail.Recurring);
        Assert.Equal(Interval.Daily, detail.Interval);
    }

    [Fact]
    public async Task CreateAsync_ThrowsNotFoundForANonMember()
    {
        await using var context = CreateContext(nameof(CreateAsync_ThrowsNotFoundForANonMember));
        SeedMembership(context, HouseholdId, OtherUserId);
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var exception = await Assert.ThrowsAsync<HouseholdFinancesException>(
            () => service.CreateAsync(HouseholdId, new BillInput("Rent", 100.00, Start, End, false, null)));

        Assert.Equal(ErrorCode.NotFound, exception.ErrorCode);
        Assert.Equal(HouseholdId, exception.Identifier);
        Assert.Empty(context.Bills);
    }

    [Fact]
    public async Task CreateAsync_ThrowsUnauthorizedWhenTheCurrentUserCannotBeResolved()
    {
        await using var context = CreateContext(nameof(CreateAsync_ThrowsUnauthorizedWhenTheCurrentUserCannotBeResolved));
        var currentUser = new StubCurrentUserService { IsAuthenticated = false, UserIdentifier = null };
        var service = new BillService(context, currentUser);

        var exception = await Assert.ThrowsAsync<HouseholdFinancesException>(
            () => service.CreateAsync(HouseholdId, new BillInput("Rent", 100.00, Start, End, false, null)));

        Assert.Equal(ErrorCode.Unauthorized, exception.ErrorCode);
        Assert.Equal(BillService.CurrentUserIdentifier, exception.Identifier);
    }

    [Fact]
    public async Task ListAsync_ReturnsEveryBillsOfTheHouseholdAndExcludesOtherHouseholds()
    {
        await using var context = CreateContext(nameof(ListAsync_ReturnsEveryBillsOfTheHouseholdAndExcludesOtherHouseholds));
        SeedMembership(context, HouseholdId, UserId);
        SeedMembership(context, HouseholdId, MemberUserId);
        SeedMembership(context, OtherHouseholdId, OtherUserId);
        context.Bills.AddRange(
            new Bill { Id = Guid.NewGuid(), Name = "Rent", Value = 1200.00, StartDate = End, EndDate = End, HouseholdId = HouseholdId, UserId = UserId },
            new Bill { Id = Guid.NewGuid(), Name = "Water", Value = 60.00, StartDate = Start, EndDate = Start, HouseholdId = HouseholdId, UserId = MemberUserId },
            new Bill { Id = Guid.NewGuid(), Name = "Other household", Value = 9999.00, StartDate = Start, EndDate = Start, HouseholdId = OtherHouseholdId, UserId = OtherUserId });
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var bills = await service.ListAsync(HouseholdId);

        Assert.Equal(2, bills.Count);
        // Ordered by start date, then name: Water (Start) precedes Rent (End).
        Assert.Equal(new[] { "Water", "Rent" }, bills.Select(bill => bill.Name));
        // A bill created by another member of the same household is still visible.
        Assert.Contains(bills, bill => bill.UserId == MemberUserId);
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
    public async Task UpdateAsync_UpdatesTheFieldsAndPreservesTheOwnerAndHousehold()
    {
        await using var context = CreateContext(nameof(UpdateAsync_UpdatesTheFieldsAndPreservesTheOwnerAndHousehold));
        SeedMembership(context, HouseholdId, UserId);
        SeedMembership(context, HouseholdId, MemberUserId);
        var billId = Guid.NewGuid();
        context.Bills.Add(new Bill
        {
            Id = billId,
            Name = "Old",
            Value = 100.00,
            StartDate = Start,
            EndDate = End,
            Recurring = false,
            Interval = Interval.Daily,
            HouseholdId = HouseholdId,
            UserId = MemberUserId
        });
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var detail = await service.UpdateAsync(
            HouseholdId,
            billId,
            new BillInput("Updated", 3200.50, Start, End, true, Interval.Monthly));

        Assert.Equal(billId, detail.Id);
        Assert.Equal("Updated", detail.Name);
        Assert.Equal(3200.50, detail.Value);
        Assert.True(detail.Recurring);
        Assert.Equal(Interval.Monthly, detail.Interval);
        // Attribution is immutable: the bill stays owned by the member who created it, and it stays
        // in the household.
        Assert.Equal(MemberUserId, detail.UserId);
        Assert.Equal(HouseholdId, detail.HouseholdId);

        var bill = Assert.Single(await service.ListAsync(HouseholdId));
        Assert.Equal("Updated", bill.Name);
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
                new BillInput("Updated", 100.00, Start, End, false, null)));

        Assert.Equal(ErrorCode.NotFound, exception.ErrorCode);
        Assert.Equal(HouseholdId, exception.Identifier);
    }

    [Fact]
    public async Task UpdateAsync_ThrowsNotFoundForABillOutsideTheHousehold()
    {
        await using var context = CreateContext(nameof(UpdateAsync_ThrowsNotFoundForABillOutsideTheHousehold));
        SeedMembership(context, HouseholdId, UserId);
        SeedMembership(context, OtherHouseholdId, OtherUserId);
        var foreignBillId = Guid.NewGuid();
        context.Bills.Add(new Bill
        {
            Id = foreignBillId,
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
                foreignBillId,
                new BillInput("Updated", 100.00, Start, End, false, null)));

        Assert.Equal(ErrorCode.NotFound, exception.ErrorCode);
        Assert.Equal(foreignBillId, exception.Identifier);
    }

    [Fact]
    public async Task DeleteAsync_RemovesTheBillAndTheListReflectsTheChange()
    {
        await using var context = CreateContext(nameof(DeleteAsync_RemovesTheBillAndTheListReflectsTheChange));
        SeedMembership(context, HouseholdId, UserId);
        SeedMembership(context, HouseholdId, MemberUserId);
        var billId = Guid.NewGuid();
        context.Bills.Add(new Bill
        {
            Id = billId,
            Name = "Rent",
            Value = 1200.00,
            StartDate = Start,
            EndDate = End,
            HouseholdId = HouseholdId,
            UserId = MemberUserId
        });
        await context.SaveChangesAsync();
        var service = CreateService(context);

        await service.DeleteAsync(HouseholdId, billId);

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
    public async Task DeleteAsync_ThrowsNotFoundForABillOutsideTheHousehold()
    {
        await using var context = CreateContext(nameof(DeleteAsync_ThrowsNotFoundForABillOutsideTheHousehold));
        SeedMembership(context, HouseholdId, UserId);
        SeedMembership(context, OtherHouseholdId, OtherUserId);
        var foreignBillId = Guid.NewGuid();
        context.Bills.Add(new Bill
        {
            Id = foreignBillId,
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
            () => service.DeleteAsync(HouseholdId, foreignBillId));

        Assert.Equal(ErrorCode.NotFound, exception.ErrorCode);
        Assert.Equal(foreignBillId, exception.Identifier);
    }

    [Fact]
    public void Constructor_RejectsNullDependencies()
    {
        using var context = CreateContext(nameof(Constructor_RejectsNullDependencies));

        Assert.Throws<ArgumentNullException>(() => new BillService(null!, new StubCurrentUserService()));
        Assert.Throws<ArgumentNullException>(() => new BillService(context, null!));
    }

    private static HouseholdFinancesDbContext CreateContext(string databaseName) =>
        new(new DbContextOptionsBuilder<HouseholdFinancesDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options);

    private static BillService CreateService(HouseholdFinancesDbContext context, Guid? userId = null) =>
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
