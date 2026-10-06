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
        CancellationToken cancellationToken = default)
    {
        // Income has no household key, so the household's membership defines which incomes belong to
        // it. A Contains subquery over the member ids avoids double counting when a member has more
        // than one row (the unique membership index prevents that in any case).
        var memberIds = _context.UserHouseholds
            .Where(membership => membership.HouseholdId == householdId)
            .Select(membership => membership.UserId);

        var incomes = await _context.Incomes
            .Where(income => memberIds.Contains(income.UserId))
            .Select(income => (double?)income.Value)
            .SumAsync(cancellationToken) ?? 0d;

        // Bills carry the household key directly.
        var payments = await _context.Bills
            .Where(bill => bill.HouseholdId == householdId)
            .Select(bill => (double?)bill.Value)
            .SumAsync(cancellationToken) ?? 0d;

        return new HouseholdTotals(incomes, payments);
    }

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
    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _context.SaveChangesAsync(cancellationToken);
}
