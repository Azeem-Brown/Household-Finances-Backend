namespace HouseholdFinances.Domain.Entities;

/// <summary>
/// A person who owns income, bills, and goals. Matches the specification "Users" schema.
/// </summary>
public class User
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Retained to match the specification schema. Authentication is third party
    /// (Google Identity), so this is unused and password hashes are never returned by the API.
    /// </summary>
    public string? Password { get; set; }
}
