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

    /// <summary>
    /// The cadence used when <see cref="Recurring"/> is true; <see langword="null"/> for a one-off
    /// goal, so a non-recurring goal is distinguishable in storage from a daily one. This extends the
    /// nullable-interval decision for Income and Bills (issue #30) to Goals so the domain stays
    /// consistent.
    /// </summary>
    public Interval? Interval { get; set; }

    public Guid HouseholdId { get; set; }

    public Guid UserId { get; set; }

    /// <summary>Amount contributed toward the goal so far.</summary>
    public double Total { get; set; }
}
