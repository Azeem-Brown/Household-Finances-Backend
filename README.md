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

## API conventions

Recorded conventions for the HTTP surface (issue #5). They are applied in code and described
in the generated OpenAPI document so new endpoints stay consistent.

| Concern | Convention |
| --- | --- |
| Style | REST over JSON. |
| Route prefix | Every controller route is served under `/api/v1`. `ApiRoutePrefixConvention` applies the prefix and is idempotent, so a controller may declare `[Route("households")]` or `[Route("api/v1/households")]` and both produce `/api/v1/households`. |
| Casing | JSON property names are camelCase and are matched case-insensitively when read. |
| Enums | Serialized as their numeric values (for example `Interval.Monthly` is `3`). The sibling frontend mirrors these values, so a string enum converter must not be added without a coordinated frontend change. |
| Date-times | UTC, ISO 8601 round-trip (for example `2026-01-02T03:04:05.0000000Z`). `UtcDateTimeJsonConverter` normalizes values to UTC on both read and write. |
| Money | A JSON number (`double`) in USD, rounded to two decimal places. |
| Identifiers | GUIDs. |
| CORS | Not configured: the Blazor Server frontend calls the API server-to-server. |

A new domain controller follows this shape. The `/api/v1` prefix is applied automatically, so
the controller only declares its own resource segment:

```csharp
[ApiController]
[Route("households")]
public class HouseholdsController : ControllerBase
{
    [HttpGet]
    public ActionResult GetHouseholds() => Ok();
}
```

### Health endpoint

`GET /health` is a liveness/readiness probe and is deliberately unversioned; it is
infrastructure rather than domain API. It returns `200 OK` with a JSON body:

```json
{ "status": "Healthy" }
```

### OpenAPI and Swagger UI

In development only, the generated OpenAPI document is served at
`http://localhost:5252/openapi/v1.json` and the Swagger UI at
`http://localhost:5252/swagger`. Both are disabled outside development.

## Authentication and authorization

Authentication and authorization are registered by `AddHouseholdFinancesAuthentication`
(`HouseholdFinances.Api.Authentication`). `UseAuthentication()` populates `HttpContext.User`, then
`UseAuthorization()` enforces the policy.

- An authenticated user is the **default requirement** for every endpoint. A controller needs no
  attribute to be protected; add `[AllowAnonymous]` (or `AllowAnonymous()` on a minimal endpoint) to
  opt out. Infrastructure endpoints (`/`, `/health`, the OpenAPI document, and the placeholder
  error-convention route) are explicitly anonymous.
- An unauthenticated request to a protected endpoint returns **HTTP 401**. A request that is
  authenticated but not permitted returns 403.
- Domain and API code read the current user through `ICurrentUserService`
  (`HouseholdFinances.Domain.Abstractions`), which exposes `IsAuthenticated` and `UserIdentifier`.
  The default `HttpContextCurrentUserService` reads the identifier from the
  `ClaimTypes.NameIdentifier` claim of the authenticated principal.

The concrete scheme is **not** configured yet. Until it is, a provider-agnostic placeholder scheme
(`DeferredAuthenticationHandler`) never authenticates a request, so the pipeline is registered and
the host still starts. The single replacement point is the marked block in
`AddHouseholdFinancesAuthentication`; the deferred authentication service issue (backend #11)
supplies the real Google Identity token validation and API-issued JWT there. No provider-specific
configuration is hard-coded.

## Error handling

Failures raise a typed exception from `HouseholdFinances.Domain.Errors`:

- `ErrorCode` is the backend error enum. Members carry explicit, stable numeric values so the
  code on the wire never changes, and they match the sibling frontend's client categories:
  `Unknown = 0`, `NotFound = 1`, `InvalidInput = 2`, `Unauthorized = 3`, `Conflict = 4`.
- `HouseholdFinancesException` carries an `ErrorCode` and the identifier or number of
  significance that was being reached. Its message follows the specification's Error Handling
  section: the enum name followed by the identifier, for example `NotFound 42`.

`ExceptionHandlingMiddleware` (`HouseholdFinances.Api.Errors`) is registered first in the
pipeline. It maps a `HouseholdFinancesException` to an HTTP status and an
`application/problem+json` body, and turns every other exception into a generic 500 without
leaking stack traces or internal details.

| ErrorCode    | HTTP status |
| ------------ | ----------- |
| NotFound     | 404         |
| InvalidInput | 400         |
| Unauthorized | 401         |
| Conflict     | 409         |
| Unknown      | 500         |

Handled errors return a ProblemDetails-shaped body that carries the numeric error code and the
identifier of significance, for example:

```json
{
  "title": "Not Found",
  "status": 404,
  "detail": "NotFound 11111111-2222-3333-4444-555555555555",
  "errorCode": 1,
  "errorName": "NotFound",
  "identifier": "11111111-2222-3333-4444-555555555555"
}
```

`GET /api/error-convention/{identifier}` is a placeholder endpoint that demonstrates the
convention; it throws `NotFound` for the supplied identifier and is replaced by real endpoints
in later issues.

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

Once the API is running:

- `GET http://localhost:5252/` returns `{"service":"HouseholdFinances.Api","status":"ok"}`.
- `GET http://localhost:5252/health` returns `{"status":"Healthy"}`.
- `GET http://localhost:5252/openapi/v1.json` returns the OpenAPI document (development only).
- `http://localhost:5252/swagger` renders the Swagger UI (development only).

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
