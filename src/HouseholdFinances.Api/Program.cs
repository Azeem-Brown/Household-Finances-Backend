using HouseholdFinances.Api.Errors;
using HouseholdFinances.Domain.Errors;
using HouseholdFinances.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

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
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Minimal endpoint so the host can be confirmed to start and respond.
// Domain, persistence, and authentication are added by later issues.
app.MapGet("/", () => Results.Ok(new { service = "HouseholdFinances.Api", status = "ok" }))
    .WithName("GetServiceInfo");

// Placeholder that demonstrates the error convention end to end: the typed exception is caught
// by ExceptionHandlingMiddleware and returned as a ProblemDetails body. Real endpoints replace
// this in later issues.
app.MapGet(
        "/api/error-convention/{identifier:guid}",
        DemonstrateErrorConvention)
    .WithName("DemonstrateErrorConvention");

app.Run();

static IResult DemonstrateErrorConvention(Guid identifier) =>
    throw new HouseholdFinancesException(ErrorCode.NotFound, identifier);
