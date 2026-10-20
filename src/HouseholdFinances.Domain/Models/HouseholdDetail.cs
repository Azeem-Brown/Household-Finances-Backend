namespace HouseholdFinances.Domain.Models;

/// <summary>
/// Read model returned for a household. <see cref="Incomes"/> and <see cref="Payments"/> are
/// computed on read from the household's entries (the members' income entries and the household's
/// bill entries), so the non-authoritative columns on the entity are never returned. Each figure is
/// the sum of the discrete entry occurrences that fall inside the requested calendar window; a
/// one-off counts once when its start date is inside the window. Money values are rounded to two
/// decimal places, half-up.
/// </summary>
/// <remarks>
/// <see cref="IncomeEntryCount"/> and <see cref="BillEntryCount"/> are the number of stored entries,
/// not occurrences, and are independent of the requested window. A household whose entries produce
/// no occurrences in the window therefore still reports non-zero counts alongside zero totals, which
/// is what distinguishes "no entries yet" from "nothing in this period".
/// </remarks>
/// <param name="Id">The household's identifier.</param>
/// <param name="Name">The household's name.</param>
/// <param name="Incomes">Total of the household members' income occurrences inside the window.</param>
/// <param name="Payments">Total of the household's bill occurrences inside the window.</param>
/// <param name="IncomeEntryCount">
/// Number of stored income entries owned by the household's members, regardless of the window.
/// </param>
/// <param name="BillEntryCount">
/// Number of stored bill entries recorded against the household, regardless of the window.
/// </param>
public sealed record HouseholdDetail(
    Guid Id,
    string Name,
    double Incomes,
    double Payments,
    int IncomeEntryCount,
    int BillEntryCount);
