using HouseholdFinances.Domain.Abstractions;
using HouseholdFinances.Domain.Entities;
using HouseholdFinances.Domain.Models;
using HouseholdFinances.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HouseholdFinances.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IRefreshTokenService"/> over
/// <see cref="HouseholdFinancesDbContext"/>. Records issued tokens, consumes a presented token with a
/// conditional update on its consumed flag, and revokes a whole family.
/// </summary>
public sealed class RefreshTokenService : IRefreshTokenService
{
    private readonly HouseholdFinancesDbContext _context;

    /// <summary>Creates the service over the supplied context.</summary>
    /// <param name="context">The EF Core context for the household finances schema.</param>
    public RefreshTokenService(HouseholdFinancesDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <inheritdoc />
    public async Task RecordIssuedAsync(
        RefreshTokenIssue issue,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(issue);

        // Create the family on first issue. A refresh reuses the existing family, so this is a no-op
        // there. The check and the insert share one unit of work.
        var familyExists = await _context.RefreshTokenFamilies
            .AnyAsync(family => family.Id == issue.FamilyId, cancellationToken);

        if (!familyExists)
        {
            _context.RefreshTokenFamilies.Add(new RefreshTokenFamily
            {
                Id = issue.FamilyId,
                UserId = issue.UserId,
                Revoked = false,
            });
        }

        // Opportunistic cleanup: drop rows that have already expired so the table does not grow
        // unbounded. The repository has no scheduler, so this happens on write.
        var expired = await _context.RefreshTokens
            .Where(token => token.ExpiresAtUtc <= issue.IssuedAtUtc)
            .ToListAsync(cancellationToken);

        if (expired.Count > 0)
        {
            _context.RefreshTokens.RemoveRange(expired);
        }

        _context.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = issue.UserId,
            FamilyId = issue.FamilyId,
            Jti = issue.Jti,
            IssuedAtUtc = issue.IssuedAtUtc,
            ExpiresAtUtc = issue.ExpiresAtUtc,
            Consumed = false,
        });

        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<RefreshTokenConsumeResult> ConsumeAsync(
        Guid userId,
        string jti,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        // Expired rows are excluded on lookup, so an expired token is never honoured.
        var token = await _context.RefreshTokens.FirstOrDefaultAsync(
            candidate => candidate.UserId == userId
                && candidate.Jti == jti
                && candidate.ExpiresAtUtc > now,
            cancellationToken);

        if (token is null)
        {
            return RefreshTokenConsumeResult.NotFound();
        }

        // A token whose family is revoked is rejected regardless of its own consumed state.
        var family = await _context.RefreshTokenFamilies
            .FirstOrDefaultAsync(candidate => candidate.Id == token.FamilyId, cancellationToken);

        if (family is null || family.Revoked)
        {
            return RefreshTokenConsumeResult.Reused(token.FamilyId);
        }

        if (token.Consumed)
        {
            return RefreshTokenConsumeResult.Reused(token.FamilyId);
        }

        // Conditional consume. Consumed is a concurrency token, so EF only updates the row when its
        // stored value still matches the value read above. If another request consumed it first, the
        // update affects no rows and EF raises a concurrency exception, which is the reuse signal.
        token.Consumed = true;
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Detach the stale entry so the caller's subsequent family revocation is not blocked by
            // re-saving it, then report reuse.
            _context.Entry(token).State = EntityState.Detached;
            return RefreshTokenConsumeResult.Reused(token.FamilyId);
        }

        return RefreshTokenConsumeResult.Consumed(token.FamilyId);
    }

    /// <inheritdoc />
    public async Task RevokeFamilyAsync(
        Guid familyId,
        CancellationToken cancellationToken = default)
    {
        var family = await _context.RefreshTokenFamilies
            .FirstOrDefaultAsync(candidate => candidate.Id == familyId, cancellationToken);

        if (family is null || family.Revoked)
        {
            return;
        }

        family.Revoked = true;
        await _context.SaveChangesAsync(cancellationToken);
    }
}
