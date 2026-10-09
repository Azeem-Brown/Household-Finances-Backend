using HouseholdFinances.Api.Models;
using HouseholdFinances.Domain.Abstractions;
using HouseholdFinances.Domain.Models;
using Microsoft.AspNetCore.Mvc;

namespace HouseholdFinances.Api.Controllers;

/// <summary>
/// Goal endpoints for a household. Goals are nested under their household because a goal belongs to
/// a household the current user must belong to; the current user is resolved from the authenticated
/// principal through <see cref="ICurrentUserService"/>, and each goal is attributed to that user. A
/// household the user does not belong to is reported as not found. A goal carries its contribution
/// total; a create starts it at zero and an update may change it.
/// </summary>
[ApiController]
[Route("households/{householdId:guid}/goals")]
public sealed class GoalsController : ControllerBase
{
    private readonly IGoalService _goalService;

    /// <summary>Creates the controller.</summary>
    /// <param name="goalService">The Goal domain operations.</param>
    public GoalsController(IGoalService goalService)
    {
        _goalService = goalService ?? throw new ArgumentNullException(nameof(goalService));
    }

    /// <summary>Creates a goal in the household, attributed to the current user.</summary>
    /// <param name="householdId">The household the goal belongs to.</param>
    /// <param name="request">The goal's fields.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The created goal.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(GoalDetail), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GoalDetail>> Create(
        Guid householdId,
        [FromBody] GoalRequest request,
        CancellationToken cancellationToken)
    {
        var goal = await _goalService.CreateAsync(householdId, request.ToInput(), cancellationToken);

        return Created($"{Request.Path}/{goal.Id}", goal);
    }

    /// <summary>Lists the household's goals.</summary>
    /// <param name="householdId">The household whose goals are listed.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The household's goals.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<GoalDetail>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<GoalDetail>>> List(
        Guid householdId,
        CancellationToken cancellationToken) =>
        Ok(await _goalService.ListAsync(householdId, cancellationToken));

    /// <summary>Updates a goal in the household. The owning user and household are not changed.</summary>
    /// <param name="householdId">The household the goal belongs to.</param>
    /// <param name="id">The goal's identifier.</param>
    /// <param name="request">The goal's replacement fields.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The updated goal.</returns>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(GoalDetail), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GoalDetail>> Update(
        Guid householdId,
        Guid id,
        [FromBody] GoalRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _goalService.UpdateAsync(householdId, id, request.ToInput(), cancellationToken));
}
