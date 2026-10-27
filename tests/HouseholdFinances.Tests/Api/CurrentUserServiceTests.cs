using System.Security.Claims;
using HouseholdFinances.Api.Authentication;
using Microsoft.AspNetCore.Http;

namespace HouseholdFinances.Tests.Api;

/// <summary>
/// Verifies the default current-user accessor resolves the identifier from the authenticated
/// principal and reports authentication correctly.
/// </summary>
public class CurrentUserServiceTests
{
    private const string UserId = "11111111-1111-1111-1111-111111111111";

    [Fact]
    public void Properties_ReturnValuesFromAnAuthenticatedPrincipal()
    {
        var service = CreateService(CreatePrincipal(authenticationType: "Test", UserId));

        Assert.True(service.IsAuthenticated);
        Assert.Equal(UserId, service.UserIdentifier);
    }

    [Fact]
    public void Properties_AreEmptyForAnAnonymousPrincipal()
    {
        var service = CreateService(CreatePrincipal(authenticationType: null, identifier: null));

        Assert.False(service.IsAuthenticated);
        Assert.Null(service.UserIdentifier);
    }

    [Fact]
    public void UserIdentifier_IsNullWhenTheAuthenticatedPrincipalHasNoIdentifierClaim()
    {
        var service = CreateService(CreatePrincipal(authenticationType: "Test", identifier: null));

        Assert.True(service.IsAuthenticated);
        Assert.Null(service.UserIdentifier);
    }

    [Fact]
    public void UserIdentifier_IsNullWhenAnAnonymousPrincipalCarriesAnIdentifierClaim()
    {
        var service = CreateService(CreatePrincipal(authenticationType: null, UserId));

        Assert.False(service.IsAuthenticated);
        Assert.Null(service.UserIdentifier);
    }

    [Fact]
    public void Properties_AreEmptyWithoutAnHttpContext()
    {
        var service = new HttpContextCurrentUserService(new HttpContextAccessor { HttpContext = null });

        Assert.False(service.IsAuthenticated);
        Assert.Null(service.UserIdentifier);
    }

    [Fact]
    public void Constructor_RejectsANullAccessor()
    {
        Assert.Throws<ArgumentNullException>(() => new HttpContextCurrentUserService(null!));
    }

    private static HttpContextCurrentUserService CreateService(ClaimsPrincipal principal) =>
        new(new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = principal } });

    private static ClaimsPrincipal CreatePrincipal(string? authenticationType, string? identifier)
    {
        var claims = identifier is null
            ? Array.Empty<Claim>()
            : new[] { new Claim(ClaimTypes.NameIdentifier, identifier) };

        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType));
    }
}
