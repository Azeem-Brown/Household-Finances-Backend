using HouseholdFinances.Api.Conventions;
using HouseholdFinances.Api.Errors;
using HouseholdFinances.Domain.Errors;
using HouseholdFinances.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// MVC controllers are the convention for domain endpoints. Every controller route is served
// under the versioned /api/v1 prefix (see ApiRoutePrefixConvention).
builder.Services
    .AddControllers(options => options.Conventions.Add(new ApiRoutePrefixConvention(ApiRoutePrefixConvention.DefaultPrefix)))
    .AddJsonOptions(options => ApiJsonConventions.Apply(options.JsonSerializerOptions));

// Minimal API endpoints share the controller JSON conventions.
builder.Services.ConfigureHttpJsonOptions(options => ApiJsonConventions.Apply(options.SerializerOptions));

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi(OpenApiConventions.Configure);

// Register the EF Core MySQL context. The connection string is read from
// configuration (appsettings placeholder, user secrets, or environment variables).
builder.Services.AddHouseholdFinancesDbContext(builder.Configuration);

var app = builder.Build();

// Global exception handling runs first so every later middleware and endpoint failure is
// translated into a ProblemDetails response.
app.UseMiddleware<ExceptionHandlingMiddleware>();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    // Serve the generated OpenAPI document and its Swagger UI in development only.
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint(OpenApiConventions.DocumentRoute, "Household Finances API v1");
        options.RoutePrefix = OpenApiConventions.SwaggerUiRoutePrefix;
    });
}

app.UseHttpsRedirection();

// Minimal endpoint so the host can be confirmed to start and respond.
// Domain, persistence, and authentication are added by later issues.
app.MapGet("/", () => Results.Ok(new { service = "HouseholdFinances.Api", status = "ok" }))
    .WithName("GetServiceInfo");

// Liveness/readiness probe. Deliberately unversioned: it is infrastructure, not domain API.
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }))
    .WithName("GetHealth")
    .WithSummary("Liveness and readiness probe for the API host.");

// Placeholder that demonstrates the error convention end to end: the typed exception is caught
// by ExceptionHandlingMiddleware and returned as a ProblemDetails body. Real endpoints replace
// this in later issues.
app.MapGet(
        "/api/error-convention/{identifier:guid}",
        DemonstrateErrorConvention)
    .WithName("DemonstrateErrorConvention");

// Controller routes are discovered here; the ApiRoutePrefixConvention wraps them in /api/v1.
app.MapControllers();

app.Run();

static IResult DemonstrateErrorConvention(Guid identifier) =>
    throw new HouseholdFinancesException(ErrorCode.NotFound, identifier);

/// <summary>Exposed so the test project can host the API with WebApplicationFactory.</summary>
public partial class Program
{
}
