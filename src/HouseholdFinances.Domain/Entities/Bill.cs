namespace HouseholdFinances.Domain.Entities;

/// <summary>
/// A recurring or one-off bill for a household. Matches the specification "Bills" schema.
/// </summary>
public class Bill
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public double Value { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public bool Recurring { get; set; }

    public Interval Interval { get; set; }

    public Guid HouseholdId { get; set; }

    public Guid UserId { get; set; }
}
