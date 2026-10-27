namespace HouseholdFinances.Domain.Entities;

/// <summary>
/// A shared household. Matches the specification "Household" schema.
/// </summary>
public class Household
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Non-authoritative total, derived from the household members' income entries.
    /// </summary>
    public double Incomes { get; set; }

    /// <summary>
    /// Non-authoritative total, derived from the household members' bill entries.
    /// </summary>
    public double Payments { get; set; }

    /// <summary>
    /// The user who created the household, or <c>null</c> when no creator was recorded. Only the
    /// creator may delete a household, so a <c>null</c> value makes the household undeletable:
    /// its members can only leave it. Rows created before this column existed are backfilled only
    /// when the household had exactly one member, because that is the only case where the creator
    /// can be identified.
    /// </summary>
    public Guid? CreatedByUserId { get; set; }
}
