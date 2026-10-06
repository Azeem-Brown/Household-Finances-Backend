namespace HouseholdFinances.Domain.Entities;

/// <summary>
/// A savings goal for a household. Matches the specification "Goals" schema, including the
/// Total field carried over from the bill implementation.
/// </summary>
public class Goal
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

    /// <summary>Amount contributed toward the goal so far.</summary>
    public double Total { get; set; }
}
