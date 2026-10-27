namespace HouseholdFinances.Domain.Models;

/// <summary>
/// Read model returned for the current user's profile. It deliberately has no password or hash
/// member: the third-party identity provider owns credentials, so the password is never returned by
/// the API.
/// </summary>
/// <param name="Id">The user's identifier.</param>
/// <param name="Name">The user's display name.</param>
/// <param name="Email">The user's email address (provider-managed and read-only here).</param>
/// <param name="Households">
/// The households the user belongs to. Empty when the user has no household membership, never
/// <c>null</c>.
/// </param>
public sealed record UserProfile(
    Guid Id,
    string Name,
    string Email,
    IReadOnlyList<HouseholdSummary> Households);
