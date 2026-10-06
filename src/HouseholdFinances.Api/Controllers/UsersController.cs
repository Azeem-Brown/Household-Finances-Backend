using HouseholdFinances.Api.Models;
using HouseholdFinances.Domain.Abstractions;
using HouseholdFinances.Domain.Models;
using Microsoft.AspNetCore.Mvc;

namespace HouseholdFinances.Api.Controllers;

/// <summary>
/// Current-user profile endpoints. The user is resolved from the authenticated principal through
/// <see cref="ICurrentUserService"/>; no endpoint accepts a user id from the client, which prevents
/// one user reading or editing another user's profile.
/// </summary>
[ApiController]
[Route("users")]
public sealed class UsersController : ControllerBase
{
    private readonly IUserService _userService;

    /// <summary>Creates the controller.</summary>
    /// <param name="userService">The User profile operations.</param>
    public UsersController(IUserService userService)
    {
        _userService = userService ?? throw new ArgumentNullException(nameof(userService));
    }

    /// <summary>Returns the current user's profile.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The current user's name, email, and household association.</returns>
    [HttpGet("me")]
    [ProducesResponseType(typeof(UserProfile), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserProfile>> GetCurrentUserProfile(CancellationToken cancellationToken) =>
        Ok(await _userService.GetCurrentUserProfileAsync(cancellationToken));

    /// <summary>Updates the current user's display name and returns the updated profile.</summary>
    /// <param name="request">The new display name.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The updated profile.</returns>
    [HttpPatch("me")]
    [ProducesResponseType(typeof(UserProfile), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserProfile>> UpdateCurrentUserDisplayName(
        [FromBody] UpdateDisplayNameRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _userService.UpdateCurrentUserDisplayNameAsync(request?.Name, cancellationToken));
}
