using HouseholdFinances.Api.Models;
using HouseholdFinances.Domain.Abstractions;
using HouseholdFinances.Domain.Models;
using Microsoft.AspNetCore.Mvc;

namespace HouseholdFinances.Api.Controllers;

/// <summary>
/// Bill endpoints for a household. Bills are nested under their household because a bill belongs to
/// a household the current user must belong to; the current user is resolved from the authenticated
/// principal through <see cref="ICurrentUserService"/>, and each bill is attributed to that user. A
/// household the user does not belong to is reported as not found.
/// </summary>
[ApiController]
[Route("households/{householdId:guid}/bills")]
public sealed class BillsController : ControllerBase
{
    private readonly IBillService _billService;

    /// <summary>Creates the controller.</summary>
    /// <param name="billService">The Bill domain operations.</param>
    public BillsController(IBillService billService)
    {
        _billService = billService ?? throw new ArgumentNullException(nameof(billService));
    }

    /// <summary>Creates a bill in the household, attributed to the current user.</summary>
    /// <param name="householdId">The household the bill belongs to.</param>
    /// <param name="request">The bill's fields.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The created bill.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(BillDetail), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BillDetail>> Create(
        Guid householdId,
        [FromBody] BillRequest request,
        CancellationToken cancellationToken)
    {
        var bill = await _billService.CreateAsync(householdId, request.ToInput(), cancellationToken);

        return Created($"{Request.Path}/{bill.Id}", bill);
    }

    /// <summary>Lists the household's bills.</summary>
    /// <param name="householdId">The household whose bills are listed.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The household's bills.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<BillDetail>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<BillDetail>>> List(
        Guid householdId,
        CancellationToken cancellationToken) =>
        Ok(await _billService.ListAsync(householdId, cancellationToken));

    /// <summary>Updates a bill in the household. The creating user is not changed.</summary>
    /// <param name="householdId">The household the bill belongs to.</param>
    /// <param name="id">The bill's identifier.</param>
    /// <param name="request">The bill's replacement fields.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The updated bill.</returns>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(BillDetail), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BillDetail>> Update(
        Guid householdId,
        Guid id,
        [FromBody] BillRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _billService.UpdateAsync(householdId, id, request.ToInput(), cancellationToken));

    /// <summary>Deletes a bill from the household.</summary>
    /// <param name="householdId">The household the bill belongs to.</param>
    /// <param name="id">The bill's identifier.</param>
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
        await _billService.DeleteAsync(householdId, id, cancellationToken);

        return NoContent();
    }
}
