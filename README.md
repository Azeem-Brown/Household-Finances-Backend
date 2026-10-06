# Household-Finances-Backend

C# API, MySQL schema, and authentication for the Household Finances family expense planner.

## Target framework

All projects target **.NET 10** (`net10.0`).

The scaffold issue suggested "a current LTS .NET (for example .NET 8)". .NET 10 is used
instead because it is a current LTS release, the installed SDK is 10.0.401, and the
sibling Blazor frontend repository (`Household-Finances-Frontend`) already targets .NET 10,
so the stack stays consistent across both repositories. The "for example .NET 8" wording in
the issue was an illustration, not a pinned requirement.

## Solution layout

| Project                            | Path                                   | Responsibility                                                      |
| ---------------------------------- | -------------------------------------- | ------------------------------------------------------------------- |
| `HouseholdFinances.Api`            | `src/HouseholdFinances.Api`            | ASP.NET Core Web API host and HTTP entry points.                    |
| `HouseholdFinances.Domain`         | `src/HouseholdFinances.Domain`         | Entity models and the `Interval` enum (service interfaces to come). |
| `HouseholdFinances.Infrastructure` | `src/HouseholdFinances.Infrastructure` | Data access; planned around EF Core with the Pomelo MySQL provider. |
| `HouseholdFinances.Tests`          | `tests/HouseholdFinances.Tests`        | xUnit test project.                                                 |

Project references: Api -> Domain and Infrastructure, Infrastructure -> Domain, and the
test project -> Api, Domain, and Infrastructure.

The Domain project defines the specification entity models and the `Interval` enum
(issue #2). The Infrastructure project now holds the EF Core `DbContext`, the entity
configurations, the Pomelo MySQL provider registration, the initial migration, and a
design-time context factory (issue #3). Controllers, services, and authentication are
added by later work.

## Prerequisites

- .NET SDK 10.0.400 or later (the scaffold was verified with 10.0.401).
- A MySQL server to apply migrations or run against a database. It is not required to build
  the solution or run the tests.

## Build, test, and run

```powershell
# Restore and build the whole solution
dotnet build

# Build and run the placeholder test
dotnet test

# Run the API (Development, HTTP profile: http://localhost:5252)
dotnet run --project src/HouseholdFinances.Api
```

Once the API is running, `GET http://localhost:5252/` returns
`{"service":"HouseholdFinances.Api","status":"ok"}`.

## Configuration and secrets

The API reads configuration from `appsettings.json`, `appsettings.Development.json`,
and the standard ASP.NET Core sources (environment variables, user secrets). Neither
committed settings file contains secrets; they hold logging and host settings only.

Store any future connection strings, API keys, or third-party authentication secrets
(Google or Auth0) outside source control, for example with
`dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<value>"` for local
development and environment variables or a secret manager for deployed environments.

### Database connection

The API host and the design-time context factory read the MySQL connection string from
the `ConnectionStrings:DefaultConnection` configuration key. `appsettings.json` holds a
credential-free placeholder (`Server=localhost;Database=household_finances;`); supply the
real value with
`dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<value>" --project src/HouseholdFinances.Api`
or the equivalent `ConnectionStrings__DefaultConnection` environment variable. No
credentials are committed.

### Migrations

The initial migration lives in `src/HouseholdFinances.Infrastructure/Migrations`. EF Core
with the Pomelo MySQL provider is used. Pomelo's current release targets EF Core 9, so the
EF Core packages are pinned to 9.0.x while every project continues to target `net10.0`.
Add or apply migrations with the EF Core tools:

```powershell
# Install the tool once (matches the pinned EF Core version)
dotnet tool install --global dotnet-ef --version 9.0.0

# Add a migration (no running database required)
dotnet ef migrations add <Name> --project src/HouseholdFinances.Infrastructure

# Apply migrations to a configured local MySQL database
dotnet ef database update --project src/HouseholdFinances.Infrastructure
```

Applying the migration requires a reachable MySQL server and a real connection string.

## Out of scope for this scaffold

Repositories, services, controllers, authentication, seed data, and database provisioning
are intentionally not part of the persistence work. EF Core with the Pomelo MySQL provider,
the initial migration, and the design-time context factory are now in place.
