namespace HouseholdFinances.Domain.Entities;

/// <summary>
/// Cadence used by recurring Income, Bills, and Goals entries.
/// The members are fixed by the recorded Interval decision:
/// Daily, Weekly, BiWeekly, Monthly, Quarterly, Yearly.
/// </summary>
public enum Interval
{
    Daily = 0,
    Weekly = 1,
    BiWeekly = 2,
    Monthly = 3,
    Quarterly = 4,
    Yearly = 5
}
