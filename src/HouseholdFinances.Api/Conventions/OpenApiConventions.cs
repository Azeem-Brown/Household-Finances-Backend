using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace HouseholdFinances.Api.Conventions;

/// <summary>
/// Describes the API's conventions in the generated OpenAPI document so the surface is
/// self-documenting for whoever consumes or extends it.
/// </summary>
public static class OpenApiConventions
{
    /// <summary>The default OpenAPI document name, which also appears in its URL.</summary>
    public const string DocumentName = "v1";

    /// <summary>The route the generated OpenAPI document is served from.</summary>
    public const string DocumentRoute = "/openapi/v1.json";

    /// <summary>The route prefix the Swagger UI is served from in development.</summary>
    public const string SwaggerUiRoutePrefix = "swagger";

    /// <summary>The conventions applied to the host's OpenAPI document.</summary>
    public const string Description =
        """
        Household Finances API, version 1.

        Conventions
        - Controller routes are served under the /api/v1 prefix.
        - JSON property names are camelCase and are matched case-insensitively when read.
        - Enums are serialized as their numeric values (for example Interval Monthly is 3).
        - Date-times are UTC and use ISO 8601 round-trip format (for example 2026-01-02T03:04:05.0000000Z).
        - Money is a JSON number (double) in USD, rounded to two decimal places.
        """;

    /// <summary>Applies the document metadata and conventions description.</summary>
    /// <param name="options">The OpenAPI options to configure.</param>
    public static void Configure(OpenApiOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.AddDocumentTransformer((document, _, _) =>
        {
            document.Info = new OpenApiInfo
            {
                Title = "Household Finances API",
                Version = DocumentName,
                Description = Description
            };

            return Task.CompletedTask;
        });
    }
}
