using HouseholdFinances.Api.Models;
using HouseholdFinances.Domain.Abstractions;
using HouseholdFinances.Domain.Models;
using Microsoft.AspNetCore.Mvc;

namespace HouseholdFinances.Api.Controllers;

/// <summary>
/// Income endpoints for a household. Income is nested under its household because the entry is
/// scoped to a household the current user must belong to; the current user is resolved from the
/// authenticated principal through <see cref="ICurrentUserService"/>, and each entry is attributed
/// to that user. A household the user does not belong to is reported as not found.
/// </summary>
[ApiController]
[Route("households/{householdId:guid}/incomes")]
public sealed class IncomesController : ControllerBase
{
    private readonly IIncomeService _incomeService;

    /// <summary>Creates the controller.</summary>
    /// <param name="incomeService">The Income domain operations.</param>
    public IncomesController(IIncomeService incomeService)
    {
        _incomeService = incomeService ?? throw new ArgumentNullException(nameof(incomeService));
    }

    /// <summary>Creates an income entry in the household, attributed to the current user.</summary>
    /// <param name="householdId">The household the entry belongs to.</param>
    /// <param name="request">The entry's fields.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The created entry.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(IncomeDetail), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IncomeDetail>> Create(
        Guid householdId,
        [FromBody] IncomeRequest request,
        CancellationToken cancellationToken)
    {
        var income = await _incomeService.CreateAsync(householdId, request.ToInput(), cancellationToken);

        return Created($"{Request.Path}/{income.Id}", income);
    }

    /// <summary>Lists the household's income entries (the entries owned by its members).</summary>
    /// <param name="householdId">The household whose entries are listed.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The household's income entries.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<IncomeDetail>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<IncomeDetail>>> List(
        Guid householdId,
        CancellationToken cancellationToken) =>
        Ok(await _incomeService.ListAsync(householdId, cancellationToken));

    /// <summary>Updates an income entry in the household. The owning user is not changed.</summary>
    /// <param name="householdId">The household the entry belongs to.</param>
    /// <param name="id">The entry's identifier.</param>
    /// <param name="request">The entry's replacement fields.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The updated entry.</returns>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(IncomeDetail), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IncomeDetail>> Update(
        Guid householdId,
        Guid id,
        [FromBody] IncomeRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _incomeService.UpdateAsync(householdId, id, request.ToInput(), cancellationToken));

    /// <summary>Deletes an income entry from the household.</summary>
    /// <param name="householdId">The household the entry belongs to.</param>
    /// <param name="id">The entry's identifier.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(
        Guid householdId,
        Guid id,
        CancellationToken cancellationToken)
    {
        await _incomeService.DeleteAsync(householdId, id, cancellationToken);

        return NoContent();
    }
}
