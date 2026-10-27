using System.Security.Claims;
using HouseholdFinances.Domain.Abstractions;

namespace HouseholdFinances.Api.Authentication;

/// <summary>
/// Default <see cref="ICurrentUserService"/> implementation. It reads the current user from
/// <see cref="HttpContext.User"/>, which the registered authentication scheme populates, so it works
/// for any scheme (JWT, OIDC, or cookie) without referencing a provider.
/// </summary>
public sealed class HttpContextCurrentUserService : ICurrentUserService
{
    /// <summary>
    /// The claim type that carries the user identifier. <see cref="ClaimTypes.NameIdentifier"/> is
    /// the standard, scheme-agnostic claim: JWT, OIDC, and cookie handlers all map their subject to
    /// it by default, so no provider-specific claim type is required.
    /// </summary>
    public const string UserIdentifierClaimType = ClaimTypes.NameIdentifier;

    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>Creates the accessor over the supplied HTTP context accessor.</summary>
    /// <param name="httpContextAccessor">Provides the current request's <see cref="HttpContext"/>.</param>
    public HttpContextCurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor
            ?? throw new ArgumentNullException(nameof(httpContextAccessor));
    }

    /// <inheritdoc />
    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    /// <inheritdoc />
    public string? UserIdentifier =>
        IsAuthenticated ? Principal?.FindFirst(UserIdentifierClaimType)?.Value : null;

    private ClaimsPrincipal? Principal => _httpContextAccessor.HttpContext?.User;
}
