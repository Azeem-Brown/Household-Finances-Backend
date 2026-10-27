namespace HouseholdFinances.Api.Models;

/// <summary>
/// Request body for updating the current user's display name. Only the display name is writable:
/// email is provider-managed and read-only, and the password is never accepted. Null, empty, or
/// whitespace is rejected by the service using the error convention.
/// </summary>
/// <param name="Name">The new display name.</param>
public sealed record UpdateDisplayNameRequest(string? Name);
