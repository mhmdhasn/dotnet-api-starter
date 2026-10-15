# StarterKit: ASP.NET Core Web API template

A reusable, production-minded **.NET 10** Web API starter. All boilerplate is already written (validation, error
handling, logging, authentication, security, pagination, auditing, endpoint registration, tests, docs and a one-command
setup) so you only add your own features.

> **Status:** generated without a .NET SDK, so it has **not been compiled or tested yet**. Run the setup script first;
> if the compiler reports anything, fix it before building on top. Details in `docs/package-decisions.md`.

## One-command setup

| OS / shell | Command |
|---|---|
| Windows (PowerShell) | `.\setup.ps1` |
| Windows (cmd / double-click) | `setup.cmd` |
| macOS / Linux | `./setup.sh` |

Options: `-SkipTests` / `--skip-tests` (skip the test step) and `-Run` / `--run` (start the API afterwards).

The script is idempotent. It: (1) checks the .NET SDK >= 10, (2) restores local tools (`dotnet-ef`), (3) restores
packages, (4) builds, (5) generates a random 64-byte `Jwt:Key` into **user-secrets** if missing, (6) creates the
`InitialCreate` migration if `Persistence/Migrations` does not exist, (7) runs the tests, (8) prints next steps.

Development uses **SQL Server LocalDB** (Windows). On macOS/Linux set the connection string to a SQL Server container:
`dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<connection string>" --project src/StarterKit.Api`.

Then: `dotnet run --project src/StarterKit.Api --launch-profile http` and open <http://localhost:5080/swagger>.

## Structure

```
StarterKit.slnx
global.json  Directory.Build.props  Directory.Packages.props  .editorconfig  .gitignore
.config/dotnet-tools.json        local dotnet-ef
setup.ps1  setup.cmd  setup.sh   one-command bootstrap
requests.http                    auth requests (VS Code REST Client / Rider / Visual Studio)
docs/package-decisions.md        package choices and rationale
src/StarterKit.Api/
  Auth/                          Identity, JWT, refresh tokens, auth endpoints
  Common/Endpoints|Validation|Errors|Middleware|Pagination|Security
  Domain/                        BaseEntity, AppUser, RefreshToken
  Persistence/                   DbContext, configurations, interceptor, seeder
  Features/                      <- your code goes here (empty on purpose)
tests/StarterKit.UnitTests/
tests/StarterKit.IntegrationTests/
```

## Adding a feature (Products example)

1. **Entity** in `Domain/` (or inside your feature folder):

   ```csharp
   public class Product : BaseEntity
   {
       public string Name { get; set; } = string.Empty;
       public decimal Price { get; set; }
   }
   ```

2. **Mapping**: add `public DbSet<Product> Products => Set<Product>();` to `AppDbContext` and a configuration:

   ```csharp
   public sealed class ProductConfiguration : BaseEntityConfiguration<Product>
   {
       protected override void ConfigureEntity(EntityTypeBuilder<Product> builder)
       {
           builder.Property(p => p.Name).IsRequired().HasMaxLength(200);
           builder.Property(p => p.Price).HasPrecision(18, 2);
       }
   }
   ```

3. **Migration**: `dotnet ef migrations add AddProducts --project src/StarterKit.Api --output-dir Persistence/Migrations`
   (set `ASPNETCORE_ENVIRONMENT=Development` so the Development connection string is used).

4. **Contracts + validator + endpoint** in `Features/Products/`:

   ```csharp
   public sealed record CreateProductRequest(string Name, decimal Price);
   public sealed record ProductResponse(Guid Id, string Name, decimal Price);

   public sealed class CreateProductRequestValidator : AbstractValidator<CreateProductRequest>
   {
       public CreateProductRequestValidator()
       {
           RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
           RuleFor(x => x.Price).GreaterThan(0);
       }
   }

   public sealed class ProductEndpoints : IEndpoint
   {
       public void Map(IEndpointRouteBuilder app)
       {
           var group = app.MapGroup("/api/products").WithTags("Products");

           group.MapGet("/", async (AppDbContext db, int? page, int? pageSize, CancellationToken ct) =>
               Results.Ok(await db.Products
                   .OrderBy(p => p.Name).ThenBy(p => p.Id)          // paging requires a stable order
                   .Select(p => new ProductResponse(p.Id, p.Name, p.Price))
                   .ToPagedResultAsync(page, pageSize, ct)));

           group.MapPost("/", async (CreateProductRequest request, AppDbContext db, CancellationToken ct) =>
               {
                   var product = new Product { Name = request.Name, Price = request.Price };
                   db.Products.Add(product);
                   await db.SaveChangesAsync(ct);
                   return Results.Created($"/api/products/{product.Id}",
                       new ProductResponse(product.Id, product.Name, product.Price));
               })
               .WithValidation<CreateProductRequest>();

           group.MapDelete("/{id:guid}", async (Guid id, AppDbContext db, CancellationToken ct) =>
               {
                   var product = await db.Products.FindAsync([id], ct)
                       ?? throw new NotFoundException($"Product {id} was not found.");
                   db.Products.Remove(product);
                   await db.SaveChangesAsync(ct);
                   return Results.NoContent();
               })
               .RequireAuthorization(Policies.AdminOnly);
       }
   }
   ```

No `Program.cs` change is needed: endpoints and validators are discovered by assembly scanning. Throw `NotFoundException`,
`ConflictException`, `ForbiddenException` or `AppException` and the global handler returns an RFC 7807 response.

## Security defaults

- **Everything requires authentication** unless the endpoint calls `.AllowAnonymous()` (fallback authorization policy).
  Health (`/health`) and the auth endpoints are anonymous. `AdminOnly` is available via `.RequireAuthorization(Policies.AdminOnly)`.
- **`Jwt__Key` in production** must come from an environment variable or secret store. The app refuses to start with a
  missing or short (< 32 chars) key. Never commit a key.
- **CORS is empty by default** (nothing allowed). Allow origins with `Cors:AllowedOrigins:0`, `Cors:AllowedOrigins:1`, ...
  (or `Cors__AllowedOrigins__0` as environment variables).
- **Optional seeded admin:** set `Seed:AdminEmail` and `Seed:AdminPassword` (user-secrets or env vars) together with
  `Database:SeedOnStartup=true`. The seeder is idempotent.
- **Roles must exist before the first registration.** `Database:SeedOnStartup` creates `Admin` and `User` (true only in
  Development). In production run the seeder once (set the flag for the first deployment) or insert the roles yourself,
  otherwise `POST /api/auth/register` fails when it assigns the `User` role.
- **Migrations in production:** run them in your deployment pipeline (for example `dotnet ef migrations bundle`), not on
  startup with several instances. `Database:MigrateOnStartup` is true only in Development.
- **Behind a reverse proxy / load balancer** add forwarded headers so rate limiting and logs see the real client IP, and
  restrict which proxies are trusted:

  ```csharp
  builder.Services.Configure<ForwardedHeadersOptions>(o =>
      o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto);
  // ... and app.UseForwardedHeaders(); as the first middleware. Configure KnownProxies/KnownNetworks.
  ```
- Response headers: `X-Content-Type-Options`, `X-Frame-Options: DENY`, `Referrer-Policy`, and a strict CSP outside
  Development; HSTS + HTTPS redirection outside Development/Testing.
- Rate limiting: global per-IP fixed window plus a stricter `auth` policy (`RateLimiting:*` settings).

## Auth endpoints

| Method | Route | Access | Description |
|---|---|---|---|
| POST | `/api/auth/register` | anonymous, `auth` rate limit | Create an account; returns tokens (201) |
| POST | `/api/auth/login` | anonymous, `auth` rate limit | Returns access + refresh token |
| POST | `/api/auth/refresh` | anonymous, `auth` rate limit | Rotates the refresh token (single use) |
| POST | `/api/auth/logout` | anonymous, `auth` rate limit | Revokes the given refresh token (204) |
| GET | `/api/auth/me` | authenticated | Current user and roles |

Reusing a revoked refresh token revokes all of that user's refresh tokens. Use `requests.http` to try the flow
(the Swagger "Authorize" button is not configured; see `docs/package-decisions.md`).

## Verification commands

```bash
dotnet restore StarterKit.slnx
dotnet build StarterKit.slnx --no-restore
dotnet test StarterKit.slnx --no-build
dotnet list StarterKit.slnx package --vulnerable --include-transitive
dotnet list StarterKit.slnx package --outdated
```

In CI set `CI=true` so warnings become errors. The integration tests use **in-memory SQLite**, which is not SQL Server;
add Testcontainers.MsSql when you depend on SQL Server specific behaviour.

## Configuration

| Key | Default | Notes |
|---|---|---|
| `ConnectionStrings:DefaultConnection` | empty (LocalDB in Development) | `ConnectionStrings__DefaultConnection` in production |
| `Jwt:Key` | **none (required, >= 32 chars)** | user-secrets locally, `Jwt__Key` in production |
| `Jwt:Issuer` / `Jwt:Audience` | `StarterKit` / `StarterKit.Clients` | change per product |
| `Jwt:AccessTokenMinutes` / `Jwt:RefreshTokenDays` | 15 / 7 | |
| `Cors:AllowedOrigins` | empty | nothing allowed by default |
| `RateLimiting:GlobalPermitLimit` / `GlobalWindowSeconds` | 100 / 60 | per IP |
| `RateLimiting:AuthPermitLimit` / `AuthWindowSeconds` | 10 / 60 | per IP, auth endpoints |
| `Database:MigrateOnStartup` | false (true in Development) | |
| `Database:SeedOnStartup` | false (true in Development) | creates roles, optional admin |
| `Seed:AdminEmail` / `Seed:AdminPassword` | empty | both required to seed an admin |
| `Serilog:*` | console, Information | standard Serilog configuration |
