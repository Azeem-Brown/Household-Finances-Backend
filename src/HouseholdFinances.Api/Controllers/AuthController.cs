using HouseholdFinances.Api.Authentication;
using HouseholdFinances.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HouseholdFinances.Api.Controllers;

/// <summary>
/// Authentication endpoints. Both actions are anonymous because they authenticate through the token
/// in the request body rather than an existing principal; the default authenticated-user policy
/// would otherwise reject them before they run.
/// </summary>
[ApiController]
[Route("auth")]
[AllowAnonymous]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthenticationService _authenticationService;

    /// <summary>Creates the controller.</summary>
    /// <param name="authenticationService">Google sign-in and token refresh.</param>
    public AuthController(IAuthenticationService authenticationService)
    {
        _authenticationService = authenticationService ?? throw new ArgumentNullException(nameof(authenticationService));
    }

    /// <summary>
    /// Exchanges a Google Identity ID token for the API's own access and refresh tokens, provisioning
    /// the user on first sign-in.
    /// </summary>
    /// <param name="request">The Google ID token from the frontend.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The API-issued token pair.</returns>
    [HttpPost("google")]
    [ProducesResponseType(typeof(AuthenticationTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthenticationTokenResponse>> SignInWithGoogle(
        [FromBody] GoogleSignInRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _authenticationService.SignInWithGoogleAsync(request?.IdToken, cancellationToken));

    /// <summary>Exchanges a refresh token for a new access and refresh token pair.</summary>
    /// <param name="request">The refresh token from a previous sign-in or refresh.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The API-issued token pair.</returns>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(AuthenticationTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthenticationTokenResponse>> Refresh(
        [FromBody] RefreshTokenRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _authenticationService.RefreshAsync(request?.RefreshToken, cancellationToken));
}
