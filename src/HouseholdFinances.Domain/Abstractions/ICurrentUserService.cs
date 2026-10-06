namespace HouseholdFinances.Domain.Abstractions;

/// <summary>
/// Resolves the identity of the user making the current request so domain and API code can depend
/// on a single abstraction instead of an authentication library or identity provider.
/// </summary>
/// <remarks>
/// This is the provider-agnostic seam for the authenticated user. The values are read from the
/// authenticated principal that the registered authentication scheme populates; the concrete
/// scheme is supplied by the deferred authentication service issue (backend #11, "Implement Google
/// Identity authentication with API-issued JWT"), which replaces the placeholder scheme. No
/// identity-provider type or configuration is referenced here.
/// </remarks>
public interface ICurrentUserService
{
    /// <summary>
    /// Gets a value indicating whether the current request carries an authenticated principal.
    /// </summary>
    bool IsAuthenticated { get; }

    /// <summary>
    /// Gets the authenticated user's identifier, or <c>null</c> when the request is unauthenticated
    /// or the principal carries no identifier claim.
    /// </summary>
    string? UserIdentifier { get; }
}
