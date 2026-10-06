using HouseholdFinances.Api.Models;
using HouseholdFinances.Domain.Abstractions;
using HouseholdFinances.Domain.Models;
using Microsoft.AspNetCore.Mvc;

namespace HouseholdFinances.Api.Controllers;

/// <summary>
/// Household endpoints. The current user is resolved from the authenticated principal through
/// <see cref="ICurrentUserService"/>; no endpoint accepts a user id from the client. Only a member
/// can read a household, and creating one also creates the current user's membership.
/// </summary>
[ApiController]
[Route("households")]
public sealed class HouseholdsController : ControllerBase
{
    private readonly IHouseholdService _householdService;

    /// <summary>Creates the controller.</summary>
    /// <param name="householdService">The Household domain operations.</param>
    public HouseholdsController(IHouseholdService householdService)
    {
        _householdService = householdService ?? throw new ArgumentNullException(nameof(householdService));
    }

    /// <summary>
    /// Creates a household and the current user's membership in it.
    /// </summary>
    /// <param name="request">The household's name.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The created household with its computed totals.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(HouseholdDetail), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<HouseholdDetail>> Create(
        [FromBody] CreateHouseholdRequest request,
        CancellationToken cancellationToken)
    {
        var household = await _householdService.CreateAsync(request?.Name, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = household.Id }, household);
    }

    /// <summary>Lists the current user's households with their computed totals.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The current user's households, ordered by name.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<HouseholdDetail>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<HouseholdDetail>>> List(CancellationToken cancellationToken) =>
        Ok(await _householdService.ListForCurrentUserAsync(cancellationToken));

    /// <summary>Gets one of the current user's households by its identifier.</summary>
    /// <param name="id">The household's identifier.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The household with its computed totals.</returns>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(HouseholdDetail), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<HouseholdDetail>> GetById(Guid id, CancellationToken cancellationToken) =>
        Ok(await _householdService.GetByIdAsync(id, cancellationToken));
}
