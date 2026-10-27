using HouseholdFinances.Domain.Abstractions;
using HouseholdFinances.Domain.Entities;
using HouseholdFinances.Domain.Models;
using HouseholdFinances.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HouseholdFinances.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IHouseholdRepository"/> over
/// <see cref="HouseholdFinancesDbContext"/>.
/// </summary>
public sealed class HouseholdRepository : IHouseholdRepository
{
    private readonly HouseholdFinancesDbContext _context;

    /// <summary>Creates the repository over the supplied context.</summary>
    /// <param name="context">The EF Core context for the household finances schema.</param>
    public HouseholdRepository(HouseholdFinancesDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <inheritdoc />
    public async Task<Household?> GetForMemberAsync(
        Guid householdId,
        Guid userId,
        CancellationToken cancellationToken = default) =>
        await (
                from membership in _context.UserHouseholds
                join household in _context.Households on membership.HouseholdId equals household.Id
                where membership.HouseholdId == householdId && membership.UserId == userId
                select household)
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<Household>> GetByMemberAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        await (
                from membership in _context.UserHouseholds
                join household in _context.Households on membership.HouseholdId equals household.Id
                where membership.UserId == userId
                orderby household.Name
                select household)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public Task<bool> MembershipExistsAsync(
        Guid householdId,
        Guid userId,
        CancellationToken cancellationToken = default) =>
        _context.UserHouseholds.AnyAsync(
            membership => membership.HouseholdId == householdId && membership.UserId == userId,
            cancellationToken);

    /// <inheritdoc />
    public async Task<HouseholdTotals> GetTotalsAsync(
        Guid householdId,
        PeriodWindow window,
        CancellationToken cancellationToken = default)
    {
        // Derive DateTime bounds from the DateOnly window so the query needs no provider-specific
        // DateOnly translation. The window bounds are inclusive, and the candidate filter uses
        // half-open reasoning: an entry can overlap the window when it starts on or before the
        // window's last day and ends on or after the window's first day.
        DateTime windowStart = window.Start.ToDateTime(TimeOnly.MinValue);
        DateTime windowEnd = window.End.ToDateTime(TimeOnly.MaxValue);

        // Income has no household key, so the household's membership defines which incomes belong to
        // it. A Contains subquery over the member ids avoids double counting when a member has more
        // than one row (the unique membership index prevents that in any case).
        IQueryable<Guid> memberIds = _context.UserHouseholds
            .Where(membership => membership.HouseholdId == householdId)
            .Select(membership => membership.UserId);

        List<Income> incomes = await _context.Incomes
            .Where(income => memberIds.Contains(income.UserId)
                && income.StartDate <= windowEnd
                && income.EndDate >= windowStart)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        // Bills carry the household key directly.
        List<Bill> bills = await _context.Bills
            .Where(bill => bill.HouseholdId == householdId
                && bill.StartDate <= windowEnd
                && bill.EndDate >= windowStart)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return new HouseholdTotals(
            SumIncomeOccurrences(incomes, window),
            SumBillOccurrences(bills, window));
    }

    /// <inheritdoc />
    public async Task<HouseholdEntryCounts> GetEntryCountsAsync(
        Guid householdId,
        CancellationToken cancellationToken = default)
    {
        // Counts are stored entries, so there is no date filter and no occurrence expansion. As in
        // GetTotalsAsync, income rows have no household key, so the household's membership defines
        // which incomes belong to it. Both counts are aggregated in the store rather than by
        // materialising the rows.
        IQueryable<Guid> memberIds = _context.UserHouseholds
            .Where(membership => membership.HouseholdId == householdId)
            .Select(membership => membership.UserId);

        int incomes = await _context.Incomes
            .CountAsync(income => memberIds.Contains(income.UserId), cancellationToken);

        int bills = await _context.Bills
            .CountAsync(bill => bill.HouseholdId == householdId, cancellationToken);

        return new HouseholdEntryCounts(incomes, bills);
    }

    private static double SumIncomeOccurrences(IEnumerable<Income> entries, PeriodWindow window) =>
        entries.Sum(entry => entry.Value * CountOccurrences(
            entry.StartDate,
            entry.EndDate,
            entry.Recurring,
            entry.Interval,
            window));

    private static double SumBillOccurrences(IEnumerable<Bill> entries, PeriodWindow window) =>
        entries.Sum(entry => entry.Value * CountOccurrences(
            entry.StartDate,
            entry.EndDate,
            entry.Recurring,
            entry.Interval,
            window));

    private static int CountOccurrences(
        DateTime startDate,
        DateTime endDate,
        bool recurring,
        Interval? interval,
        PeriodWindow window) =>
        OccurrenceExpander.Expand(startDate, endDate, recurring, interval, window).Count;

    /// <inheritdoc />
    public Task AddAsync(Household household, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(household);

        _context.Households.Add(household);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task AddMembershipAsync(UserHousehold membership, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(membership);

        _context.UserHouseholds.Add(membership);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<HouseholdLogo?> GetLogoAsync(Guid householdId, CancellationToken cancellationToken = default) =>
        _context.HouseholdLogos
            .AsNoTracking()
            .FirstOrDefaultAsync(logo => logo.HouseholdId == householdId, cancellationToken);

    /// <inheritdoc />
    public async Task UpsertLogoAsync(HouseholdLogo logo, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(logo);

        // Tracked so a replace updates the single existing row rather than attempting a duplicate
        // primary key. Both the household identifier and the image are keyed together.
        HouseholdLogo? existing = await _context.HouseholdLogos
            .FirstOrDefaultAsync(candidate => candidate.HouseholdId == logo.HouseholdId, cancellationToken);

        if (existing is null)
        {
            _context.HouseholdLogos.Add(logo);
            return;
        }

        existing.Content = logo.Content;
        existing.ContentType = logo.ContentType;
        existing.UpdatedAtUtc = logo.UpdatedAtUtc;
    }

    /// <inheritdoc />
    public async Task<bool> RemoveLogoAsync(Guid householdId, CancellationToken cancellationToken = default)
    {
        HouseholdLogo? existing = await _context.HouseholdLogos
            .FirstOrDefaultAsync(logo => logo.HouseholdId == householdId, cancellationToken);

        if (existing is null)
        {
            return false;
        }

        _context.HouseholdLogos.Remove(existing);
        return true;
    }

    /// <inheritdoc />
    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _context.SaveChangesAsync(cancellationToken);
}
