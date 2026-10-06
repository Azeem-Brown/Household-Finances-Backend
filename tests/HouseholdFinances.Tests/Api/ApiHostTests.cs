using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace HouseholdFinances.Tests.Api;

/// <summary>
/// Exercises the live HTTP surface: the health endpoint, the OpenAPI document served in
/// development, and the pre-existing endpoints whose behavior must be preserved.
/// </summary>
public class ApiHostTests : IClassFixture<ApiHostTests.Factory>
{
    private readonly Factory _factory;

    public ApiHostTests(Factory factory) => _factory = factory;

    [Fact]
    public async Task GetHealth_ReturnsSuccessResponse()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Healthy", document.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task GetOpenApiDocument_DocumentsTheApiInDevelopment()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("Household Finances API", json, StringComparison.Ordinal);
        Assert.Contains("/health", json, StringComparison.Ordinal);
        Assert.Contains("/api/v1", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetSwaggerUI_IsServedInDevelopment()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/swagger/index.html");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetServiceInfo_ReturnsServiceStatus()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("HouseholdFinances.Api", document.RootElement.GetProperty("service").GetString());
        Assert.Equal("ok", document.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task GetErrorConvention_PreservesTheProblemDetailsShape()
    {
        using var client = _factory.CreateClient();
        var id = Guid.Parse("11111111-2222-3333-4444-555555555555");

        using var response = await client.GetAsync($"/api/error-convention/{id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(1, document.RootElement.GetProperty("errorCode").GetInt32());
        Assert.Equal("NotFound", document.RootElement.GetProperty("errorName").GetString());
        Assert.Equal(id.ToString(), document.RootElement.GetProperty("identifier").GetString());
        Assert.Equal($"NotFound {id}", document.RootElement.GetProperty("detail").GetString());
    }

    /// <summary>Hosts the API with the Development environment so OpenAPI and the UI are mapped.</summary>
    public sealed class Factory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                // Keeps the host buildable without a real MySQL connection string; the context is
                // registered lazily and never connects during these tests.
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = "Server=localhost;Database=household_finances;"
                });
            });
        }
    }
}
