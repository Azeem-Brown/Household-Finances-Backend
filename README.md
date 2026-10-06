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
| `HouseholdFinances.Domain`         | `src/HouseholdFinances.Domain`         | Entities and service interfaces (none yet).                         |
| `HouseholdFinances.Infrastructure` | `src/HouseholdFinances.Infrastructure` | Data access; planned around EF Core with the Pomelo MySQL provider. |
| `HouseholdFinances.Tests`          | `tests/HouseholdFinances.Tests`        | xUnit test project.                                                 |

Project references: Api -> Domain and Infrastructure, Infrastructure -> Domain, and the
test project -> Api, Domain, and Infrastructure.

The Domain and Infrastructure projects are intentionally empty scaffolds. Entity
definitions, EF Core wiring, controllers, and authentication are excluded from this issue
and are added by later work.

## Prerequisites

- .NET SDK 10.0.400 or later (the scaffold was verified with 10.0.401).
- A MySQL server, once the persistence work lands. Not required to build or run this scaffold.

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

## Out of scope for this scaffold

Entity definitions, EF Core database wiring, controllers, authentication, and CI/CD
pipelines are intentionally not part of this issue.
