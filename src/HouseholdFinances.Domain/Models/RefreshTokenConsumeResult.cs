namespace HouseholdFinances.Domain.Models;

/// <summary>
/// The result of attempting to consume a presented refresh token.
/// </summary>
public enum RefreshTokenConsumeOutcome
{
    /// <summary>
    /// The presented token matched a live row and was marked consumed by this call. It is the only
    /// outcome that may lead to a new token pair.
    /// </summary>
    Consumed = 0,

    /// <summary>
    /// The presented token matched a row that was already consumed, or its family was revoked, or a
    /// concurrent request won the race to consume it. This is the reuse signal: the caller must
    /// revoke the whole family and reject the request.
    /// </summary>
    Reused = 1,

    /// <summary>
    /// No live (unexpired) row matched the presented token. Nothing is revoked.
    /// </summary>
    NotFound = 2
}

/// <summary>
/// The outcome of consuming a presented refresh token, together with the family it belongs to so the
/// caller can revoke the whole family when reuse is detected.
/// </summary>
/// <param name="Outcome">What happened when the token was consumed.</param>
/// <param name="FamilyId">
/// The family the presented token belonged to, or <see langword="null"/> when no live row matched.
/// </param>
public sealed record RefreshTokenConsumeResult(RefreshTokenConsumeOutcome Outcome, Guid? FamilyId)
{
    /// <summary>The presented token matched a live row and was consumed by this call.</summary>
    public static RefreshTokenConsumeResult Consumed(Guid familyId) =>
        new(RefreshTokenConsumeOutcome.Consumed, familyId);

    /// <summary>The presented token was already used, or its family was revoked.</summary>
    public static RefreshTokenConsumeResult Reused(Guid familyId) =>
        new(RefreshTokenConsumeOutcome.Reused, familyId);

    /// <summary>No live row matched the presented token.</summary>
    public static RefreshTokenConsumeResult NotFound() =>
        new(RefreshTokenConsumeOutcome.NotFound, null);
}
