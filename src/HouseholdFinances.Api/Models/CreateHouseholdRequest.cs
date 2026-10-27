namespace HouseholdFinances.Api.Models;

/// <summary>
/// Request body for creating a household. Only the name is accepted; the current user and the
/// membership are resolved by the server. Null, empty, or whitespace is rejected by the service
/// using the error convention.
/// </summary>
/// <param name="Name">The household's name.</param>
public sealed record CreateHouseholdRequest(string? Name);
