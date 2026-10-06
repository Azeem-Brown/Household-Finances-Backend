using System.Reflection;
using HouseholdFinances.Api.Conventions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace HouseholdFinances.Tests.Api;

/// <summary>
/// Verifies the route prefix convention that gives every controller its <c>/api/v1</c> prefix.
/// </summary>
public class ApiRoutePrefixConventionTests
{
    [Fact]
    public void Apply_PrefixesRelativeControllerRoutes()
    {
        var controller = CreateController("households");

        new ApiRoutePrefixConvention(ApiRoutePrefixConvention.DefaultPrefix).Apply(ToApplication(controller));

        Assert.Equal("api/v1/households", controller.Selectors[0].AttributeRouteModel!.Template);
    }

    [Fact]
    public void Apply_AcceptsAPrefixWithSurroundingSlashes()
    {
        var controller = CreateController("households");

        new ApiRoutePrefixConvention("/api/v1/").Apply(ToApplication(controller));

        Assert.Equal("api/v1/households", controller.Selectors[0].AttributeRouteModel!.Template);
    }

    [Fact]
    public void Apply_DoesNotDoublePrefixARouteAlreadyUnderThePrefix()
    {
        var controller = CreateController("api/v1/households");

        new ApiRoutePrefixConvention(ApiRoutePrefixConvention.DefaultPrefix).Apply(ToApplication(controller));

        Assert.Equal("api/v1/households", controller.Selectors[0].AttributeRouteModel!.Template);
    }

    [Fact]
    public void Apply_LeavesAFullyQualifiedRouteUnderThePrefix()
    {
        var controller = CreateController("/api/v1/households");

        new ApiRoutePrefixConvention(ApiRoutePrefixConvention.DefaultPrefix).Apply(ToApplication(controller));

        // The route already resolves under /api/v1, so it is left exactly as declared.
        Assert.Equal("/api/v1/households", controller.Selectors[0].AttributeRouteModel!.Template);
    }

    [Fact]
    public void Apply_GivesARouteToAControllerThatDeclaresNone()
    {
        var controller = new ControllerModel(typeof(StubController).GetTypeInfo(), Array.Empty<object>());

        new ApiRoutePrefixConvention(ApiRoutePrefixConvention.DefaultPrefix).Apply(ToApplication(controller));

        Assert.Equal("api/v1", controller.Selectors[0].AttributeRouteModel!.Template);
    }

    [Fact]
    public void Apply_RejectsABlankPrefix()
    {
        Assert.Throws<ArgumentException>(() => new ApiRoutePrefixConvention(" "));
    }

    private static ControllerModel CreateController(string route)
    {
        var controller = new ControllerModel(typeof(StubController).GetTypeInfo(), Array.Empty<object>());
        controller.Selectors.Add(new SelectorModel
        {
            AttributeRouteModel = new AttributeRouteModel(new RouteAttribute(route))
        });

        return controller;
    }

    private static ApplicationModel ToApplication(ControllerModel controller)
    {
        var application = new ApplicationModel();
        application.Controllers.Add(controller);

        return application;
    }

    private sealed class StubController
    {
    }
}
