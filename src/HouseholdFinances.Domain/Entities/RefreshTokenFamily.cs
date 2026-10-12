namespace HouseholdFinances.Domain.Entities;

/// <summary>
/// A refresh-token family: the chain of refresh tokens descended from a single sign-in. Every token
/// issued at sign-in and at each successful refresh belongs to the same family, so a family is the
/// unit of revocation. When reuse of a consumed token is detected the whole family is revoked, which
/// invalidates every token in the chain and forces re-authentication. The family identifier is
/// generated server-side at issue time and is never exposed to the client.
/// </summary>
public class RefreshTokenFamily
{
    /// <summary>
    /// The family identifier. It is carried by every token row issued in the family.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The user the family was issued to. A family never spans users.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Whether the family has been revoked. Once revoked it is never un-revoked; every token in the
    /// family is rejected from that point on, so later refreshes from the family fail.
    /// </summary>
    public bool Revoked { get; set; }
}
