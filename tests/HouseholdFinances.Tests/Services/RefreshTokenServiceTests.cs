using HouseholdFinances.Domain.Entities;
using HouseholdFinances.Domain.Models;
using HouseholdFinances.Infrastructure.Persistence;
using HouseholdFinances.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace HouseholdFinances.Tests.Services;

/// <summary>
/// Verifies the refresh-token store: a token is recorded on issue, consumed exactly once, and reuse
/// of a consumed token revokes the whole family. Also covers expired-row handling and cleanup, and
/// the concurrency path.
/// </summary>
/// <remarks>
/// These tests use the EF Core in-memory provider, which does not enforce unique constraints or
/// provide true transactional isolation. The concurrency test therefore exercises the consume logic
/// but cannot prove the database-level atomicity guarantee; that is demonstrated separately against
/// the real database.
/// </remarks>
public class RefreshTokenServiceTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid FamilyId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    // The clock is pinned so expiry is evaluated at a controlled instant. The fixture used the wall
    // clock before, so its tokens expired on 2026-10-19 and four tests began failing without any code
    // change. A pinned instant also lets a test state an expiry boundary explicitly.
    private static readonly DateTimeOffset PinnedNow = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);
    private static readonly FixedTimeProvider Clock = new(PinnedNow);
    private static readonly DateTime Now = PinnedNow.UtcDateTime;
    private const string Jti = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";

    [Fact]
    public void Constructor_RejectsNullDependencies()
    {
        using var context = CreateContext(nameof(Constructor_RejectsNullDependencies));

        Assert.Throws<ArgumentNullException>(() => new RefreshTokenService(null!, Clock));
        Assert.Throws<ArgumentNullException>(() => new RefreshTokenService(context, null!));
    }

    [Fact]
    public async Task RecordIssuedAsync_RecordsTheTokenAndCreatesItsFamily()
    {
        await using var context = CreateContext(nameof(RecordIssuedAsync_RecordsTheTokenAndCreatesItsFamily));
        var service = new RefreshTokenService(context, Clock);

        await service.RecordIssuedAsync(new RefreshTokenIssue(UserId, Jti, FamilyId, Now, Now.AddDays(7)));

        var token = Assert.Single(await context.RefreshTokens.ToListAsync());
        Assert.Equal(UserId, token.UserId);
        Assert.Equal(FamilyId, token.FamilyId);
        Assert.Equal(Jti, token.Jti);
        Assert.False(token.Consumed);

        var family = Assert.Single(await context.RefreshTokenFamilies.ToListAsync());
        Assert.Equal(FamilyId, family.Id);
        Assert.Equal(UserId, family.UserId);
        Assert.False(family.Revoked);
    }

    [Fact]
    public async Task RecordIssuedAsync_ReusesAnExistingFamily()
    {
        await using var context = CreateContext(nameof(RecordIssuedAsync_ReusesAnExistingFamily));
        var service = new RefreshTokenService(context, Clock);

        await service.RecordIssuedAsync(new RefreshTokenIssue(UserId, Jti, FamilyId, Now, Now.AddDays(7)));
        await service.RecordIssuedAsync(new RefreshTokenIssue(UserId, "next-jti", FamilyId, Now, Now.AddDays(7)));

        Assert.Equal(2, await context.RefreshTokens.CountAsync());
        Assert.Equal(1, await context.RefreshTokenFamilies.CountAsync());
    }

    [Fact]
    public async Task ConsumeAsync_FirstUse_ConsumesTheToken()
    {
        await using var context = CreateContext(nameof(ConsumeAsync_FirstUse_ConsumesTheToken));
        var service = new RefreshTokenService(context, Clock);
        await service.RecordIssuedAsync(new RefreshTokenIssue(UserId, Jti, FamilyId, Now, Now.AddDays(7)));

        var result = await service.ConsumeAsync(UserId, Jti);

        Assert.Equal(RefreshTokenConsumeOutcome.Consumed, result.Outcome);
        Assert.Equal(FamilyId, result.FamilyId);
        Assert.True((await context.RefreshTokens.SingleAsync()).Consumed);
    }

    [Fact]
    public async Task ConsumeAsync_SecondUse_ReportsReused()
    {
        await using var context = CreateContext(nameof(ConsumeAsync_SecondUse_ReportsReused));
        var service = new RefreshTokenService(context, Clock);
        await service.RecordIssuedAsync(new RefreshTokenIssue(UserId, Jti, FamilyId, Now, Now.AddDays(7)));

        await service.ConsumeAsync(UserId, Jti);
        var second = await service.ConsumeAsync(UserId, Jti);

        Assert.Equal(RefreshTokenConsumeOutcome.Reused, second.Outcome);
        Assert.Equal(FamilyId, second.FamilyId);
    }

    [Fact]
    public async Task ConsumeAsync_UnknownJti_ReportsNotFound()
    {
        await using var context = CreateContext(nameof(ConsumeAsync_UnknownJti_ReportsNotFound));
        var service = new RefreshTokenService(context, Clock);
        await service.RecordIssuedAsync(new RefreshTokenIssue(UserId, Jti, FamilyId, Now, Now.AddDays(7)));

        var result = await service.ConsumeAsync(UserId, "not-a-recorded-jti");

        Assert.Equal(RefreshTokenConsumeOutcome.NotFound, result.Outcome);
        Assert.Null(result.FamilyId);
    }

    [Fact]
    public async Task ConsumeAsync_ForAnotherUser_ReportsNotFound()
    {
        await using var context = CreateContext(nameof(ConsumeAsync_ForAnotherUser_ReportsNotFound));
        var service = new RefreshTokenService(context, Clock);
        await service.RecordIssuedAsync(new RefreshTokenIssue(UserId, Jti, FamilyId, Now, Now.AddDays(7)));

        var result = await service.ConsumeAsync(Guid.NewGuid(), Jti);

        Assert.Equal(RefreshTokenConsumeOutcome.NotFound, result.Outcome);
    }

    [Fact]
    public async Task ConsumeAsync_ExpiredRow_IsNotHonoured()
    {
        await using var context = CreateContext(nameof(ConsumeAsync_ExpiredRow_IsNotHonoured));
        context.RefreshTokenFamilies.Add(new RefreshTokenFamily { Id = FamilyId, UserId = UserId, Revoked = false });
        context.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            FamilyId = FamilyId,
            Jti = Jti,
            IssuedAtUtc = Now.AddDays(-10),
            ExpiresAtUtc = Now.AddDays(-3),
            Consumed = false,
        });
        await context.SaveChangesAsync();
        var service = new RefreshTokenService(context, Clock);

        var result = await service.ConsumeAsync(UserId, Jti);

        Assert.Equal(RefreshTokenConsumeOutcome.NotFound, result.Outcome);
        // The expired row was not consumed.
        Assert.False((await context.RefreshTokens.SingleAsync()).Consumed);
    }

    [Theory]
    [InlineData(1, RefreshTokenConsumeOutcome.Consumed)]
    [InlineData(-1, RefreshTokenConsumeOutcome.NotFound)]
    public async Task ConsumeAsync_EvaluatesExpiryAgainstTheInjectedClock(
        int secondsFromPinnedNow,
        RefreshTokenConsumeOutcome expected)
    {
        // The token sits one second inside, or one second past, the pinned instant. The wall clock
        // plays no part, so the boundary is explicit rather than relative to when the test runs.
        await using var context = CreateContext(
            $"{nameof(ConsumeAsync_EvaluatesExpiryAgainstTheInjectedClock)}_{secondsFromPinnedNow}");
        context.RefreshTokenFamilies.Add(new RefreshTokenFamily { Id = FamilyId, UserId = UserId, Revoked = false });
        context.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            FamilyId = FamilyId,
            Jti = Jti,
            IssuedAtUtc = Now.AddDays(-1),
            ExpiresAtUtc = Now.AddSeconds(secondsFromPinnedNow),
            Consumed = false,
        });
        await context.SaveChangesAsync();
        var service = new RefreshTokenService(context, Clock);

        var result = await service.ConsumeAsync(UserId, Jti);

        Assert.Equal(expected, result.Outcome);
    }

    [Fact]
    public async Task RecordIssuedAsync_RemovesExpiredRows()
    {
        await using var context = CreateContext(nameof(RecordIssuedAsync_RemovesExpiredRows));
        context.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            FamilyId = FamilyId,
            Jti = "expired-jti",
            IssuedAtUtc = Now.AddDays(-10),
            ExpiresAtUtc = Now.AddDays(-3),
            Consumed = false,
        });
        await context.SaveChangesAsync();
        var service = new RefreshTokenService(context, Clock);

        await service.RecordIssuedAsync(new RefreshTokenIssue(UserId, Jti, FamilyId, Now, Now.AddDays(7)));

        var remaining = await context.RefreshTokens.ToListAsync();
        var token = Assert.Single(remaining);
        Assert.Equal(Jti, token.Jti);
    }

    [Fact]
    public async Task RevokeFamilyAsync_RevokesTheFamilyAndRejectsItsTokens()
    {
        await using var context = CreateContext(nameof(RevokeFamilyAsync_RevokesTheFamilyAndRejectsItsTokens));
        var service = new RefreshTokenService(context, Clock);
        await service.RecordIssuedAsync(new RefreshTokenIssue(UserId, Jti, FamilyId, Now, Now.AddDays(7)));
        await service.RecordIssuedAsync(new RefreshTokenIssue(UserId, "other-jti", FamilyId, Now, Now.AddDays(7)));

        await service.RevokeFamilyAsync(FamilyId);

        Assert.True((await context.RefreshTokenFamilies.SingleAsync()).Revoked);

        // Every token in the family is rejected, consumed or not.
        Assert.Equal(RefreshTokenConsumeOutcome.Reused, (await service.ConsumeAsync(UserId, Jti)).Outcome);
        Assert.Equal(RefreshTokenConsumeOutcome.Reused, (await service.ConsumeAsync(UserId, "other-jti")).Outcome);
    }

    [Fact]
    public async Task RevokeFamilyAsync_IsIdempotent()
    {
        await using var context = CreateContext(nameof(RevokeFamilyAsync_IsIdempotent));
        var service = new RefreshTokenService(context, Clock);
        await service.RecordIssuedAsync(new RefreshTokenIssue(UserId, Jti, FamilyId, Now, Now.AddDays(7)));

        await service.RevokeFamilyAsync(FamilyId);
        await service.RevokeFamilyAsync(FamilyId);
        await service.RevokeFamilyAsync(Guid.NewGuid());

        Assert.True((await context.RefreshTokenFamilies.SingleAsync()).Revoked);
    }

    [Fact]
    public async Task ConsumeAsync_TwoOverlappingConsumes_YieldOneConsumedAndOneReused()
    {
        var databaseName = nameof(ConsumeAsync_TwoOverlappingConsumes_YieldOneConsumedAndOneReused);

        await using (var seedContext = CreateContext(databaseName))
        {
            await new RefreshTokenService(seedContext, Clock)
                .RecordIssuedAsync(new RefreshTokenIssue(UserId, Jti, FamilyId, Now, Now.AddDays(7)));
        }

        await using var firstContext = CreateContext(databaseName);
        await using var secondContext = CreateContext(databaseName);
        var first = new RefreshTokenService(firstContext, Clock);
        var second = new RefreshTokenService(secondContext, Clock);

        var results = await Task.WhenAll(
            first.ConsumeAsync(UserId, Jti),
            second.ConsumeAsync(UserId, Jti));

        Assert.Equal(1, results.Count(result => result.Outcome == RefreshTokenConsumeOutcome.Consumed));
        Assert.Equal(1, results.Count(result => result.Outcome == RefreshTokenConsumeOutcome.Reused));
    }

    private static HouseholdFinancesDbContext CreateContext(string databaseName) =>
        new(new DbContextOptionsBuilder<HouseholdFinancesDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options);
}
