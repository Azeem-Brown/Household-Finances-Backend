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

## User profile endpoints

The User domain is implemented as `IUserRepository` and `IUserService`
(`HouseholdFinances.Domain.Abstractions`) with their EF Core implementations in
`HouseholdFinances.Infrastructure`, exposed by `UsersController`
(`HouseholdFinances.Api.Controllers`). The current user is resolved from the authenticated
principal through `ICurrentUserService`; no endpoint accepts a user id from the client.

- `GET /api/v1/users/me` returns the current user's `id`, `name`, `email`, and `households`
  (an array of `{ id, name }`, empty when the user has no membership).
- `PATCH /api/v1/users/me` updates the current user's display name from a `{ "name": "..." }`
  body and returns the updated profile.

Both endpoints require an authenticated caller. The password/hash is never returned: the response
is the `UserProfile` read model, which has no password member. A blank or whitespace-only name is
rejected with `InvalidInput Name` (HTTP 400), an unknown user with `NotFound <id>` (HTTP 404), and
an unresolvable caller with `Unauthorized me` (HTTP 401), all using the error convention.

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

The Google identity sign-in endpoint validates an incoming Google ID token through
`IGoogleTokenValidator`, after which the API issues its own JWT access and refresh tokens and the
bearer scheme validates them on every request. The real validator is `GoogleIdentityTokenValidator`,
which verifies the token against Google's published signing certificates and the configured client
id.

### Development Google token bypass

For local testing without real Google credentials the API can accept a locally generated token of
the form `dev:<subject>` (for example `dev:local-user-1`) in place of a real Google ID token; the
remainder of the token becomes the signed-in user's Google subject. The bypass is **off by default**
and is honored only in the **Development** environment. Enable it per developer, never in a
committed settings file, with user secrets:

```powershell
dotnet user-secrets set "Authentication:Google:DevelopmentBypass:Enabled" "true" --project src/HouseholdFinances.Api
```

or the equivalent environment variable:

```powershell
$env:Authentication__Google__DevelopmentBypass__Enabled = "true"
```

With the bypass off (the default) a `dev:` token is rejected because the real validator is used, so
sign-in requires a genuine Google ID token. Enabling the setting outside Development makes the host
refuse to start with an `InvalidOperationException` that names the setting, and while the bypass is
active the host logs a warning at startup.

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
- A MySQL 8 server (8.0.21 or newer) to apply migrations or run against a database. It is not
  required to build the solution or run the tests. See
  [Local development database](#local-development-database) to provision one.

## Local development database

The database-backed endpoints and the EF Core migrations need a MySQL 8 server (8.0.21 or
newer, see `MySqlServerVersionDefaults`). The tests do not: they use
`Microsoft.EntityFrameworkCore.InMemory` and pass without a database.

Two provisioning options are documented. **Native MySQL is the path that was verified end to
end against a real database**; Docker Compose is offered as an alternative.

### 1. Start a MySQL 8 server

**Option A - native MySQL (verified).** Install MySQL Server 8.0 or later and start it. Run
the following once as a MySQL administrator to create the development database and a
dedicated, non-secret development account:

```sql
CREATE DATABASE IF NOT EXISTS household_finances
  CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;
CREATE USER IF NOT EXISTS 'household_dev'@'localhost' IDENTIFIED BY 'devpassword';
GRANT ALL PRIVILEGES ON household_finances.* TO 'household_dev'@'localhost';
FLUSH PRIVILEGES;
```

The `household_dev` / `devpassword` credentials are local development values only and must
never be used in a deployed environment.

**Option B - Docker Compose (alternative).** The repository root contains `compose.yaml`,
which defines a MySQL 8 service with a named volume, the `household_finances` database, and
the same non-secret development credentials. With Docker installed:

```powershell
docker compose up -d
```

The service publishes MySQL on host port 3306. If a native MySQL already occupies 3306, see
[Troubleshooting](#troubleshooting) before starting the container.

### 2. Set the connection string

The connection string is read from the `ConnectionStrings:DefaultConnection` configuration
key and is never committed. Supply it with user secrets:

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost;Database=household_finances;Uid=household_dev;Pwd=devpassword;" --project src/HouseholdFinances.Api
```

or with the equivalent environment variable:

```powershell
$env:ConnectionStrings__DefaultConnection = "Server=localhost;Database=household_finances;Uid=household_dev;Pwd=devpassword;"
```

The `HouseholdFinances.Api` and `HouseholdFinances.Infrastructure` projects share one local
user-secrets store, so the single `dotnet user-secrets set ... --project
src/HouseholdFinances.Api` command configures both the running API host and the `dotnet ef`
design-time factory. `appsettings.json` holds only a credential-free placeholder
(`Server=localhost;Database=household_finances;`) used when generating migrations without a
database.

### 3. Apply the migrations

```powershell
dotnet tool install --global dotnet-ef --version 9.0.0
dotnet ef database update --project src/HouseholdFinances.Infrastructure --startup-project src/HouseholdFinances.Api
```

### 4. Run the API against the database

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet run --project src/HouseholdFinances.Api --no-launch-profile --urls http://localhost:5252
```

In Development the API can accept a development token of the form `dev:<subject>` in place of a
real Google ID token, so an authenticated call can be exercised without Google credentials. This
bypass is **off by default** and must be opted in per developer (see
[Development Google token bypass](#development-google-token-bypass)); enable it before running the
host:

```powershell
dotnet user-secrets set "Authentication:Google:DevelopmentBypass:Enabled" "true" --project src/HouseholdFinances.Api
```

Then open `src/HouseholdFinances.Api/HouseholdFinances.Api.http` and run the sign-in request
followed by the `/api/v1/users/me` request.

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

The API host and the design-time context factory read the MySQL connection string from the
`ConnectionStrings:DefaultConnection` configuration key and share one local user-secrets
store. Set it once with user secrets (`dotnet user-secrets set
"ConnectionStrings:DefaultConnection" "<value>" --project src/HouseholdFinances.Api`) or the
equivalent `ConnectionStrings__DefaultConnection` environment variable, as described in
[Local development database](#local-development-database). No credentials are committed.

### Migrations

EF Core with the Pomelo MySQL provider is used. Pomelo's current release targets EF Core 9,
so the EF Core packages are pinned to 9.0.x while every project continues to target
`net10.0`. The migrations live in `src/HouseholdFinances.Infrastructure/Migrations`. Add or
apply them with the EF Core tools:

```powershell
# Install the tool once (matches the pinned EF Core version)
dotnet tool install --global dotnet-ef --version 9.0.0

# Add a migration (no running database required)
dotnet ef migrations add <Name> --project src/HouseholdFinances.Infrastructure

# Apply migrations to a configured local MySQL database
dotnet ef database update --project src/HouseholdFinances.Infrastructure --startup-project src/HouseholdFinances.Api
```

Applying the migrations requires a reachable MySQL server and a real connection string; see
[Local development database](#local-development-database). Adding a migration needs the
connection string to be resolvable but not a running server, so it works with the
credential-free placeholder.

## Troubleshooting

- **Server not reachable (for example "Unable to connect to any of the specified MySQL
  hosts").** Confirm the server is running and listening on the configured host and port
  (`Get-Service *mysql*` on Windows, or `Test-NetConnection localhost -Port 3306`), and that
  the host and port in `ConnectionStrings:DefaultConnection` match the server.
- **Wrong credentials ("Access denied for user ...").** Confirm the user and password in the
  connection string match the database, and re-run the `CREATE USER` / `GRANT` statements in
  [Local development database](#local-development-database). The API host and the `dotnet ef`
  design-time factory both read the same `ConnectionStrings:DefaultConnection` value, so fix
  it in one place (user secrets or the environment variable).
- **Migrations not applied (for example "Table 'household_finances.users' doesn't exist").**
  Run `dotnet ef database update` (see [Local development database](#local-development-database)).
  List applied migrations with `dotnet ef migrations list --project
  src/HouseholdFinances.Infrastructure`.
- **Host port 3306 already in use ("bind: address already in use").** Port 3306 is the MySQL
  default, and a native MySQL service already occupies it. Either stop the native service
  before `docker compose up -d`, or remap the published port in `compose.yaml` (for example
  `"3307:3306"`) and update the port in the connection string to match. Only one process can
  bind host port 3306 at a time.

## Out of scope for this scaffold

Repositories, services, controllers, authentication, seed data, and database provisioning
are intentionally not part of the persistence work. EF Core with the Pomelo MySQL provider,
the initial migration, and the design-time context factory are now in place.
