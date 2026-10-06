namespace HouseholdFinances.Domain.Models;

/// <summary>
/// Summary of a household the current user belongs to. Only the fields the profile needs are
/// exposed; the Household domain itself (service, repository, controller) is implemented by a later
/// issue.
/// </summary>
/// <param name="Id">The household's identifier.</param>
/// <param name="Name">The household's name.</param>
public sealed record HouseholdSummary(Guid Id, string Name);
