# Package decisions

This document records **which NuGet packages are used, which were rejected, and why**. Rule of thumb: the smallest
sensible dependency set, preferring built-in ASP.NET Core features, with every package justified.

> **Verification status (read this first)**
> - **Verified online while building this kit:** .NET 10 is the current LTS (released 11 Nov 2025, supported until
>   14 Nov 2028); `Serilog.AspNetCore 10.0.0` exists; `Swashbuckle.AspNetCore 10.0.1` exists (newer 10.1.x releases
>   also exist and may be adopted after a review of their release notes).
> - **Not verified (pinned to the safest known version):** FluentValidation 12.0.0, the Microsoft packages on the
>   `10.0.0` GA line, Microsoft.NET.Test.Sdk 17.14.1, xunit 2.9.3, xunit.runner.visualstudio 3.0.2.
> - **Nothing has been restored, built or tested yet.** The environment this kit was generated in had no .NET SDK and
>   no NuGet access. Run `setup.ps1` / `setup.sh` and fix anything the compiler reports. Then run
>   `dotnet list package --vulnerable --include-transitive` and `dotnet list package --outdated`.
> - The original "Section 2.1 – NuGet Package Evaluation and Selection" text was not supplied. Sections A–J below follow
>   the topics named in the build brief; reconcile them with your spec if it differs.

## Legend

| Status | Meaning |
|---|---|
| **Installed** | Referenced by a project in this solution |
| **Optional** | Documented only; add when the need appears |
| **Rejected** | Considered and deliberately not used |

## Summary table

| Package | Status | Scope | Project |
|---|---|---|---|
| FluentValidation 12.0.0 | Installed | Runtime | StarterKit.Api |
| FluentValidation.DependencyInjectionExtensions 12.0.0 | Installed | Runtime | StarterKit.Api |
| Microsoft.EntityFrameworkCore.SqlServer 10.0.0 | Installed | Runtime | StarterKit.Api |
| Microsoft.AspNetCore.Identity.EntityFrameworkCore 10.0.0 | Installed | Runtime | StarterKit.Api |
| Microsoft.AspNetCore.Authentication.JwtBearer 10.0.0 | Installed | Runtime | StarterKit.Api |
| Serilog.AspNetCore 10.0.0 | Installed | Runtime | StarterKit.Api |
| Swashbuckle.AspNetCore 10.0.1 | Installed | Runtime (Development UI) | StarterKit.Api |
| Microsoft.EntityFrameworkCore.Design 10.0.0 | Installed | Dev-only (`PrivateAssets=all`) | StarterKit.Api |
| Microsoft.NET.Test.Sdk 17.14.1 | Installed | Tests | both test projects |
| xunit 2.9.3 | Installed | Tests | both test projects |
| xunit.runner.visualstudio 3.0.2 | Installed | Tests | both test projects |
| Microsoft.AspNetCore.Mvc.Testing 10.0.0 | Installed | Tests | IntegrationTests |
| Microsoft.EntityFrameworkCore.Sqlite 10.0.0 | Installed | Tests | IntegrationTests |
| dotnet-ef 10.0.0 | Installed | Local tool (`.config/dotnet-tools.json`) | solution |
| Microsoft.AspNetCore.OpenApi | Rejected | – | – |
| Microsoft.EntityFrameworkCore.InMemory | Rejected | – | – |
| AutoMapper / Mapster | Rejected | – | – |
| StyleCop.Analyzers | Rejected | – | – |
| Mocking libraries (Moq, NSubstitute) | Rejected (for now) | – | – |
| Microsoft.Extensions.Http.Resilience | Optional | Runtime | – |
| OpenTelemetry (ASP.NET Core / EF Core / OTLP) | Optional | Runtime | – |
| Asp.Versioning.Http | Optional | Runtime | – |
| Testcontainers.MsSql | Optional | Tests | – |

## Pre-built infrastructure (no package needed)

| Capability | Where | Built-in feature used |
|---|---|---|
| Endpoint registration | `Common/Endpoints` | Minimal APIs, `IEndpointRouteBuilder`, reflection scan |
| Request validation | `Common/Validation` | `IEndpointFilter` + FluentValidation |
| Error handling | `Common/Errors` | `IExceptionHandler`, `IProblemDetailsService` (RFC 7807) |
| Correlation IDs | `Common/Middleware` | Middleware + Serilog `LogContext` |
| Security headers | `Common/Middleware` | Middleware, HSTS, HTTPS redirection |
| CORS | `Common/Security` | `Microsoft.AspNetCore.Cors` (deny by default) |
| Rate limiting | `Common/Security` | `Microsoft.AspNetCore.RateLimiting` (global per-IP + `auth` policy) |
| Health checks | `Program.cs` | `AddHealthChecks` / `MapHealthChecks("/health")` |
| Pagination | `Common/Pagination` | EF Core `Skip`/`Take`/`CountAsync` |
| Auditing | `Persistence` | `SaveChangesInterceptor` + `TimeProvider` |
| Authorization | `Auth` | Fallback policy (secure by default) + `AdminOnly` policy |

---

## A. Validation

**Installed:** FluentValidation + FluentValidation.DependencyInjectionExtensions.
Minimal APIs have no built-in validation pipeline comparable to FluentValidation's rule model, so a validator per request
type plus the `ValidationFilter<T>` endpoint filter gives consistent `400` responses (`ValidationProblem`).
Registering validators alone does **not** validate anything; the filter (`.WithValidation<T>()`) does. An integration
test (`Register_WithInvalidData_Returns400WithFieldErrors`) proves it.
**Rejected:** DataAnnotations on request DTOs (poor composition, no async rules). `JwtOptions` still uses
DataAnnotations because options validation is the built-in mechanism there.

## B. API documentation

**Installed:** Swashbuckle.AspNetCore only (one coherent approach), exposed in Development.
**Rejected:** Microsoft.AspNetCore.OpenApi, because it overlaps with Swashbuckle; running both produces two competing
documents.
**Known gap:** the Swagger **"Authorize" button is not configured.** Swashbuckle 10 moved to Microsoft.OpenApi 2.x and
changed its security API; this could not be compiled here, so a guessed implementation was not shipped. Use
`requests.http` for authenticated calls, or add the bearer scheme once you can compile against Swashbuckle 10.

## C. Logging

**Installed:** Serilog.AspNetCore (brings the Console sink, configuration reader, request logging).
Configured from the `Serilog` section of appsettings; request logging enabled; every log line carries `CorrelationId`.
`CreateBootstrapLogger` is intentionally **not** used because it breaks tests that create several
`WebApplicationFactory` instances.
**Optional:** more sinks (File, Seq, OpenTelemetry) via configuration; for containers prefer JSON console output
(`Serilog.Formatting.Compact`, already a transitive dependency).

## D. Persistence

**Installed:** EF Core SQL Server (retry on failure enabled), EF Core Design (dev-only), `dotnet-ef` as a *local* tool so
every developer and CI uses the same version.
Pre-built: `BaseEntity` (GUID v7 id, `CreatedAt`, `UpdatedAt`), audit interceptor, `AddAppDbContext(...)` where the
caller chooses the provider, idempotent `DbSeeder`, `Database:MigrateOnStartup` / `Database:SeedOnStartup`.
**Rejected:** EF Core InMemory (not relational: no constraints, no transactions, hides real bugs).
**Production note:** do not rely on `MigrateOnStartup` with several instances; run migrations in the deployment pipeline
(e.g. `dotnet ef migrations bundle`).

## E. Authentication and authorization

**Installed:** ASP.NET Core Identity (EF stores) and JwtBearer, because authentication was explicitly requested.

- **Access tokens:** HS256 JWT, 15 minutes, claims `sub`, `jti`, `email`, `role`, created with `JsonWebTokenHandler`.
- **Signing key management:** `Jwt:Key` must be at least 32 characters, is validated at startup, and is **never** in
  source control. Locally it lives in user-secrets (the setup script generates 64 random bytes). In production supply
  it through `Jwt__Key` or a secret store (Key Vault, etc.). To rotate: deploy a new key; all existing access tokens
  become invalid (acceptable with 15-minute lifetimes) and clients re-authenticate or refresh. Zero-downtime rotation
  with multiple keys (`kid`) is not implemented.
- **Expiry:** access 15 min (30 s clock skew), refresh 7 days; both configurable (`Jwt:*`).
- **Refresh tokens:** 64 random bytes (Base64Url), only the SHA-256 hash is stored, single-use rotation with
  `ReplacedByTokenHash`. The rotation step is an atomic conditional update, so two concurrent refreshes cannot both win.
- **Revocation:** logout revokes the presented refresh token. **Presenting an already-revoked token revokes all of that
  user's tokens** (theft detection). Access tokens already issued stay valid until they expire (stateless JWT).
- **Secure by default:** the fallback authorization policy requires an authenticated user; health and auth endpoints
  opt out with `.AllowAnonymous()`. `AdminOnly` policy is provided.
- **Login:** the same generic `401` for unknown user, wrong password and lockout (5 attempts, 15 minutes).

**Not included yet:** email confirmation, password reset / forgot password, MFA, external logins (Google, etc.),
refresh-token cleanup job for expired rows, access-token denylist, device/session management.
**Known limitations:** registration returns `409` for an existing email (account enumeration); login does not equalise
timing for unknown users. Decide whether these matter for your threat model.

## F. Resilience

**Not installed.** There are no outbound HTTP calls yet. **Optional:** `Microsoft.Extensions.Http.Resilience`
(retry, timeout, circuit breaker for `HttpClient`) when the first integration with an external API appears.
SQL transient failures are already covered by `EnableRetryOnFailure`.

## G. Object mapping

**Rejected:** AutoMapper and Mapster. Manual mapping (static `ToResponse()` extension methods or constructors on
records) is explicit, compile-checked and trivially debuggable for the size of API this kit targets.

## H. Testing strategy

- **Unit tests (xunit):** validators, `TokenService`, `Paging`. No mocking library is needed yet (**rejected for now**);
  hand-written fakes and `TimeProvider` cover the current needs.
- **Integration tests:** `WebApplicationFactory<Program>` on **in-memory SQLite** with the connection kept open;
  environment `Testing`, in-memory configuration for `Jwt:Key` and very high rate limits.
- **Limitations:** SQLite is not SQL Server (different type mapping, no `DateTimeOffset` comparison translation, no
  rowversion). Add **Testcontainers.MsSql** (optional) when you rely on SQL Server specific behaviour, raw SQL, or
  concurrency tokens.

## I. Code quality

Built-in .NET analyzers only (`AnalysisLevel=latest`, `EnforceCodeStyleInBuild=true`), `.editorconfig`, nullable
reference types, `NuGetAudit` (all packages, including transitive, all severities), central package management, and
`TreatWarningsAsErrors` only when `CI=true`.
**Rejected:** StyleCop.Analyzers (mostly formatting noise that `.editorconfig` already covers).

## J. Other capabilities

- **API versioning:** optional, `Asp.Versioning.Http`, when you need to run `v1` and `v2` side by side.
- **Observability:** optional, OpenTelemetry (traces/metrics for ASP.NET Core, EF Core, HttpClient) + an OTLP exporter.
- **Time:** `TimeProvider` is registered and used instead of `DateTime.UtcNow` so time is testable.
- **IDs:** GUID v7 (`Guid.CreateVersion7`), time-ordered and index-friendly.
- **Behind a reverse proxy:** configure `ForwardedHeaders` (see README) so rate limiting and logs see the real client IP.
- **Caching, background jobs, email, file storage, message bus:** deliberately not included.
