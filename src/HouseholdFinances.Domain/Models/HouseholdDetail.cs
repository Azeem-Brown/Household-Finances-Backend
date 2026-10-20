namespace HouseholdFinances.Domain.Models;

/// <summary>
/// Read model returned for a household. <see cref="Incomes"/> and <see cref="Payments"/> are
/// computed on read from the household's entries (the members' income entries and the household's
/// bill entries), so the non-authoritative columns on the entity are never returned. Each figure is
/// the sum of the discrete entry occurrences that fall inside the requested calendar window; a
/// one-off counts once when its start date is inside the window. Money values are rounded to two
/// decimal places, half-up.
/// </summary>
/// <param name="Id">The household's identifier.</param>
/// <param name="Name">The household's name.</param>
/// <param name="Incomes">Total of the household members' income occurrences inside the window.</param>
/// <param name="Payments">Total of the household's bill occurrences inside the window.</param>
public sealed record HouseholdDetail(Guid Id, string Name, double Incomes, double Payments);
