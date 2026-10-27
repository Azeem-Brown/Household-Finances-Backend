using HouseholdFinances.Domain.Models;

namespace HouseholdFinances.Domain.Abstractions;

/// <summary>
/// Persistence access for issued refresh tokens and their families. It records a token when it is
/// issued, atomically consumes one when it is presented, and revokes a whole family when reuse is
/// detected. The token value is never passed to or stored by this abstraction; only the JWT's
/// non-secret identifier is used.
/// </summary>
public interface IRefreshTokenService
{
    /// <summary>
    /// Records a newly issued refresh token, creating its family when it does not already exist.
    /// Expired rows are removed opportunistically so the table does not grow unbounded.
    /// </summary>
    /// <param name="issue">The token to record.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    Task RecordIssuedAsync(
        RefreshTokenIssue issue,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically consumes the presented token. A live, unconsumed, unrevoked row is marked consumed
    /// and reported as <see cref="RefreshTokenConsumeOutcome.Consumed"/>. A row that is already
    /// consumed or whose family is revoked, or a row that a concurrent request consumed first, is
    /// reported as <see cref="RefreshTokenConsumeOutcome.Reused"/>. Expired rows and unknown
    /// identifiers are reported as <see cref="RefreshTokenConsumeOutcome.NotFound"/>.
    /// </summary>
    /// <param name="userId">The user the token was issued to.</param>
    /// <param name="jti">The JWT identifier of the presented token.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The outcome and, when a row matched, the family it belongs to.</returns>
    Task<RefreshTokenConsumeResult> ConsumeAsync(
        Guid userId,
        string jti,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes the whole family, invalidating every token in it. Idempotent: revoking an already
    /// revoked or unknown family does nothing.
    /// </summary>
    /// <param name="familyId">The family to revoke.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    Task RevokeFamilyAsync(
        Guid familyId,
        CancellationToken cancellationToken = default);
}
