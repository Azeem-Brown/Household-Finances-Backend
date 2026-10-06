namespace HouseholdFinances.Domain.Models;

/// <summary>
/// Raw (unrounded) totals for a household, computed from its entries. The repository returns these
/// sums and the service applies the money rounding rule, so the rounding decision lives in one
/// place.
/// </summary>
/// <param name="Incomes">Sum of the household members' income entry values.</param>
/// <param name="Payments">Sum of the household's bill entry values.</param>
public sealed record HouseholdTotals(double Incomes, double Payments);
