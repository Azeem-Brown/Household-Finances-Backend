namespace HouseholdFinances.Domain.Models;

/// <summary>
/// The details of a refresh token that has been issued and must be recorded for single-use
/// enforcement. The token value itself is deliberately absent: the persistence layer only needs the
/// identifier it can later match on, never the bearer credential.
/// </summary>
/// <param name="UserId">The user the token was issued to.</param>
/// <param name="Jti">The JWT's unique identifier, matched when the token is presented.</param>
/// <param name="FamilyId">The family the token belongs to.</param>
/// <param name="IssuedAtUtc">The UTC instant the token was issued.</param>
/// <param name="ExpiresAtUtc">The UTC instant the token expires.</param>
public sealed record RefreshTokenIssue(
    Guid UserId,
    string Jti,
    Guid FamilyId,
    DateTime IssuedAtUtc,
    DateTime ExpiresAtUtc);
