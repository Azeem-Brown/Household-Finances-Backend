namespace HouseholdFinances.Domain.Models;

/// <summary>
/// The number of stored entries that belong to a household. Unlike <see cref="HouseholdTotals"/>,
/// these counts are not derived from a calendar window and do not expand recurrence: one recurring
/// entry counts once however many occurrences it produces. They let a client distinguish a
/// household with no entries from one whose entries simply fall outside the requested period.
/// </summary>
/// <param name="Incomes">Number of stored income entries owned by the household's members.</param>
/// <param name="Bills">Number of stored bill entries recorded against the household.</param>
public sealed record HouseholdEntryCounts(int Incomes, int Bills);
