namespace HouseholdFinances.Domain.Entities;

/// <summary>
/// A labeled income entry owned by a user. Matches the specification "Income" schema.
/// Income is linked to a household through the owning user's membership, so it has no
/// household key of its own.
/// </summary>
public class Income
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public double Value { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public bool Recurring { get; set; }

    public Interval Interval { get; set; }

    public Guid UserId { get; set; }
}
