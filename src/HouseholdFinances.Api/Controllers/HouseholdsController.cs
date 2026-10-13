using HouseholdFinances.Api.Models;
using HouseholdFinances.Domain.Abstractions;
using HouseholdFinances.Domain.Errors;
using HouseholdFinances.Domain.Models;
using HouseholdFinances.Infrastructure.Services;
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

    /// <summary>
    /// Sets or replaces the household's logo. The current user must be a member of the household.
    /// The image is validated before anything is written, so a rejected upload leaves any existing
    /// logo untouched.
    /// </summary>
    /// <param name="id">The household's identifier.</param>
    /// <param name="file">The image, as multipart form data.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>No content when the logo was stored.</returns>
    [HttpPut("{id:guid}/logo")]
    [RequestSizeLimit(HouseholdService.MaxLogoUploadRequestBytes)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetLogo(
        Guid id,
        [FromForm] IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (file is null)
        {
            // Reported through the shared error convention, so the client sees the same shape as
            // every other validation failure.
            throw new HouseholdFinancesException(ErrorCode.InvalidInput, HouseholdService.LogoIdentifier);
        }

        byte[] content;
        await using (var buffer = new MemoryStream())
        {
            await using var source = file.OpenReadStream();
            await source.CopyToAsync(buffer, cancellationToken);
            content = buffer.ToArray();
        }

        await _householdService.SetLogoAsync(id, content, file.ContentType, cancellationToken);

        return NoContent();
    }

    /// <summary>
    /// Returns the household's logo image. The current user must be a member of the household.
    /// </summary>
    /// <param name="id">The household's identifier.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The image, or not found when the household has no logo.</returns>
    [HttpGet("{id:guid}/logo")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLogo(Guid id, CancellationToken cancellationToken)
    {
        var logo = await _householdService.GetLogoAsync(id, cancellationToken);

        // A member and a non-member both receive not found when there is nothing to return, so a
        // probe cannot distinguish an absent logo from a household the caller cannot see.
        return logo is null
            ? NotFound()
            : File(logo.Content, logo.ContentType);
    }

    /// <summary>
    /// Removes the household's logo. The current user must be a member of the household.
    /// </summary>
    /// <param name="id">The household's identifier.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>No content, whether or not a logo was present.</returns>
    [HttpDelete("{id:guid}/logo")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveLogo(Guid id, CancellationToken cancellationToken)
    {
        await _householdService.RemoveLogoAsync(id, cancellationToken);

        return NoContent();
    }
}
