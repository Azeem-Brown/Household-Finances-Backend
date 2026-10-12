namespace HouseholdFinances.Domain.Entities;

/// <summary>
/// A single issued refresh token, recorded so the server can enforce single use. The signed JWT
/// carries a stable <c>jti</c>; the matching row is looked up by that identifier when the token is
/// presented at the refresh endpoint. A row is written on every issue (sign-in and each refresh) and
/// marked consumed exactly once, the first time it is exchanged. Its
/// <see cref="FamilyId"/> ties it to the <see cref="RefreshTokenFamily"/> that owns revocation.
/// </summary>
public class RefreshToken
{
    /// <summary>The row identifier. Not exposed to the client.</summary>
    public Guid Id { get; set; }

    /// <summary>The user the token was issued to.</summary>
    public Guid UserId { get; set; }

    /// <summary>The family this token belongs to; the unit of revocation.</summary>
    public Guid FamilyId { get; set; }

    /// <summary>
    /// The JWT's unique identifier (<c>jti</c>). It is the value a presented token is matched on,
    /// and it is unique across the table so a token cannot be recorded twice.
    /// </summary>
    public string Jti { get; set; } = string.Empty;

    /// <summary>The UTC instant the token was issued. Used for opportunistic cleanup.</summary>
    public DateTime IssuedAtUtc { get; set; }

    /// <summary>The UTC instant the token expires. Expired rows are never honoured on lookup.</summary>
    public DateTime ExpiresAtUtc { get; set; }

    /// <summary>
    /// Whether the token has been exchanged. A conditional update on this flag is what makes the
    /// first use win: a token is consumed exactly once, and a second use is treated as reuse. The
    /// flag is configured as a concurrency token so two near-simultaneous refreshes cannot both
    /// consume the same row.
    /// </summary>
    public bool Consumed { get; set; }
}
