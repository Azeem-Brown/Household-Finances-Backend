namespace HouseholdFinances.Domain.Models;

/// <summary>
/// Read model returned for a household. <see cref="Incomes"/> and <see cref="Payments"/> are
/// computed from the household's entries on read (the members' income entries and the household's
/// bill entries), so the non-authoritative columns on the entity are never returned. Money values
/// are rounded to two decimal places, half-up.
/// </summary>
/// <param name="Id">The household's identifier.</param>
/// <param name="Name">The household's name.</param>
/// <param name="Incomes">Computed total of the household members' income entries.</param>
/// <param name="Payments">Computed total of the household's bill entries.</param>
public sealed record HouseholdDetail(Guid Id, string Name, double Incomes, double Payments);
