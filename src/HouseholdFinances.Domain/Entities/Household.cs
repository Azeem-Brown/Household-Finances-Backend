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
}
