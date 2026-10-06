using HouseholdFinances.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Register the EF Core MySQL context. The connection string is read from
// configuration (appsettings placeholder, user secrets, or environment variables).
builder.Services.AddHouseholdFinancesDbContext(builder.Configuration);

var app = builder.Build();

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

app.Run();
