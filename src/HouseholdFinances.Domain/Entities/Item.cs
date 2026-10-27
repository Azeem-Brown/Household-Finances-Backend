namespace HouseholdFinances.Domain.Entities;

/// <summary>
/// A priced item belonging to a household. Matches the specification "Items" schema.
/// </summary>
public class Item
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Optional free-text description; null when the specification allows no description.</summary>
    public string? Description { get; set; }

    public double Price { get; set; }

    public Guid HouseholdId { get; set; }
}
