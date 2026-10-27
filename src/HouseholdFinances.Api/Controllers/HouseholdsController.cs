using HouseholdFinances.Api.Models;
using HouseholdFinances.Domain.Abstractions;
using HouseholdFinances.Domain.Entities;
using HouseholdFinances.Domain.Errors;
using HouseholdFinances.Domain.Models;
using Microsoft.AspNetCore.Mvc;

namespace HouseholdFinances.Api.Controllers;

/// <summary>
/// Household endpoints. The current user is resolved from the authenticated principal through
/// <see cref="ICurrentUserService"/>; no endpoint accepts a user id from the client. Only a member
/// can read a household, and creating one also creates the current user's membership. Household
/// totals cover the entry occurrences inside the calendar window selected by the optional
/// <c>period</c> query parameter, which defaults to <see cref="Interval.Monthly"/>. The response
/// also reports the number of stored income and bill entries, which does not depend on
/// <c>period</c>, so a household with entries outside the selected window is distinguishable from
/// one with no entries at all.
/// </summary>
[ApiController]
[Route("households")]
public sealed class HouseholdsController : ControllerBase
{
    /// <summary>
    /// Identifier of significance reported when the <c>period</c> query value is not a defined
    /// <see cref="Interval"/> member name.
    /// </summary>
    public const string PeriodIdentifier = "Period";

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
        HouseholdDetail household = await _householdService.CreateAsync(request?.Name, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = household.Id }, household);
    }

    /// <summary>Lists the current user's households with their windowed totals.</summary>
    /// <param name="period">
    /// The calendar period whose window is totalled. One of <c>Daily</c>, <c>Weekly</c>,
    /// <c>BiWeekly</c>, <c>Monthly</c>, <c>Quarterly</c>, or <c>Yearly</c>, matched
    /// case-insensitively. Omitted or empty means <c>Monthly</c>.
    /// </param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The current user's households, ordered by name.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<HouseholdDetail>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<HouseholdDetail>>> List(
        [FromQuery] string? period,
        CancellationToken cancellationToken) =>
        Ok(await _householdService.ListForCurrentUserAsync(ParsePeriod(period), cancellationToken));

    /// <summary>Gets one of the current user's households by its identifier.</summary>
    /// <param name="id">The household's identifier.</param>
    /// <param name="period">
    /// The calendar period whose window is totalled. One of <c>Daily</c>, <c>Weekly</c>,
    /// <c>BiWeekly</c>, <c>Monthly</c>, <c>Quarterly</c>, or <c>Yearly</c>, matched
    /// case-insensitively. Omitted or empty means <c>Monthly</c>.
    /// </param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The household with its windowed totals.</returns>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(HouseholdDetail), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<HouseholdDetail>> GetById(
        Guid id,
        [FromQuery] string? period,
        CancellationToken cancellationToken) =>
        Ok(await _householdService.GetByIdAsync(id, ParsePeriod(period), cancellationToken));

    // The query value is the enum member name, matched case-insensitively, and parsed here rather
    // than in a model binder so an unknown value stays on the existing problem+json error convention
    // instead of producing a different model-binding error shape. The names are matched explicitly
    // because Enum.TryParse also accepts the underlying number, which would make "3" silently mean
    // Monthly; the contract is the name only.
    private static Interval ParsePeriod(string? period)
    {
        if (string.IsNullOrWhiteSpace(period))
        {
            return Interval.Monthly;
        }

        string name = period.Trim();

        foreach (string candidate in Enum.GetNames<Interval>())
        {
            if (string.Equals(candidate, name, StringComparison.OrdinalIgnoreCase))
            {
                return Enum.Parse<Interval>(candidate);
            }
        }

        throw new HouseholdFinancesException(ErrorCode.InvalidInput, PeriodIdentifier);
    }
}
