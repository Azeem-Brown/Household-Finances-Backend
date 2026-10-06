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
    /// The stable Google Identity subject (<c>sub</c>) this user was provisioned from, or
    /// <c>null</c> for rows created before Google Identity sign-in was wired up. Users are keyed by
    /// subject, never by email, because an email address can change or be reassigned. Unique when
    /// present, so one Google account maps to exactly one user.
    /// </summary>
    public string? GoogleSubject { get; set; }

    /// <summary>
    /// Retained to match the specification schema. Authentication is third party
    /// (Google Identity), so this is unused and password hashes are never returned by the API.
    /// </summary>
    public string? Password { get; set; }
}
