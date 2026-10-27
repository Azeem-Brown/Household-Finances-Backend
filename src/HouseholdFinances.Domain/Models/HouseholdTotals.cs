namespace HouseholdFinances.Domain.Models;

/// <summary>
/// Raw (unrounded) totals for a household, computed from the entry occurrences that fall inside a
/// requested calendar window. The repository returns these sums and the service applies the money
/// rounding rule, so the rounding decision lives in one place.
/// </summary>
/// <param name="Incomes">Sum of the household members' income occurrences inside the window.</param>
/// <param name="Payments">Sum of the household's bill occurrences inside the window.</param>
public sealed record HouseholdTotals(double Incomes, double Payments);
