using HouseholdFinances.Domain.Abstractions;
using HouseholdFinances.Domain.Entities;
using HouseholdFinances.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HouseholdFinances.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IUserRepository"/> over
/// <see cref="HouseholdFinancesDbContext"/>.
/// </summary>
public sealed class UserRepository : IUserRepository
{
    private readonly HouseholdFinancesDbContext _context;

    /// <summary>Creates the repository over the supplied context.</summary>
    /// <param name="context">The EF Core context for the household finances schema.</param>
    public UserRepository(HouseholdFinancesDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <inheritdoc />
    public Task<User?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _context.Users.FirstOrDefaultAsync(user => user.Id == userId, cancellationToken);

    /// <inheritdoc />
    public Task<User?> GetByGoogleSubjectAsync(
        string googleSubject,
        CancellationToken cancellationToken = default) =>
        _context.Users.FirstOrDefaultAsync(
            user => user.GoogleSubject == googleSubject,
            cancellationToken);

    /// <inheritdoc />
    public void Add(User user) =>
        _context.Users.Add(user ?? throw new ArgumentNullException(nameof(user)));

    /// <inheritdoc />
    public async Task<IReadOnlyList<Household>> GetHouseholdsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        // A read-only projection joined to Household for its name; the Household domain itself is
        // implemented by a later issue. Ordered by name so the response is deterministic.
        var households = await (
                from membership in _context.UserHouseholds
                join household in _context.Households on membership.HouseholdId equals household.Id
                where membership.UserId == userId
                orderby household.Name
                select household)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return households;
    }

    /// <inheritdoc />
    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _context.SaveChangesAsync(cancellationToken);
}
