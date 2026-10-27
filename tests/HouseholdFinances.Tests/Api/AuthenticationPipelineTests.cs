using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HouseholdFinances.Tests.Api;

/// <summary>
/// Verifies the authentication/authorization pipeline rejects unauthenticated requests to a
/// protected endpoint with HTTP 401.
/// </summary>
public class AuthenticationPipelineTests
{
    [Fact]
    public async Task ProtectedEndpoint_WithoutAuthentication_ReturnsUnauthorized()
    {
        using var factory = new Factory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/v1/test-protected");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// A controller that exists only in the test assembly, added as an application part, so the
    /// default authenticated-user requirement can be exercised before any real domain controller
    /// exists. It declares no authorization attribute, so it is protected by the fallback policy.
    /// </summary>
    [ApiController]
    [Route("test-protected")]
    public sealed class TestProtectedController : ControllerBase
    {
        [HttpGet]
        public IActionResult Get() => Ok();
    }

    public sealed class Factory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                // Keeps the host buildable without a real MySQL connection string.
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = "Server=localhost;Database=household_finances;"
                });
            });

            // Registers the test controller so the API's MVC conventions (including the /api/v1
            // route prefix and the default authorization requirement) apply to it.
            builder.ConfigureServices(services =>
                services.AddControllers().AddApplicationPart(typeof(TestProtectedController).Assembly));
        }
    }
}
