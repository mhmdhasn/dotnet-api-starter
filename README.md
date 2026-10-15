# StarterKit — ASP.NET Core Web API

A reusable **.NET 10 Web API starter template** with common API infrastructure already set up.

Use it to start new backend projects without rebuilding the same foundation every time.

## What's Included?

| Feature | What it does |
|---|---|
| Authentication | Register, login, JWT access tokens, and refresh tokens |
| Validation | Validate incoming requests |
| Error handling | Return consistent API error responses |
| Database | Entity Framework Core with SQL Server |
| Logging | Structured logging with Serilog |
| Security | Authorization, rate limiting, security headers, and CORS configuration |
| Pagination | Reusable paginated responses |
| Testing | Unit tests and integration tests |
| API documentation | OpenAPI documentation |
| Setup scripts | Automate the initial setup |

**Important:** The template has not been compiled or tested yet. Run the setup script and resolve any errors before using it as the foundation for a real project.

See [`docs/package-decisions.md`](docs/package-decisions.md) for package choices and configuration details.

---

## 1. Requirements

Before you start, install:

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Git
- SQL Server, or a SQL Server container if you're using macOS or Linux

**Note:** The default Development database uses SQL Server LocalDB, which is intended for Windows.

---

## 2. Run the Project

Choose the command for your operating system.

| Operating system | Command |
|---|---|
| Windows PowerShell | `.\setup.ps1` |
| Windows CMD | `setup.cmd` |
| macOS / Linux | `./setup.sh` |

On macOS or Linux, make the script executable first if needed:

```bash
chmod +x setup.sh
./setup.sh
```

The setup script automatically:

1. Checks the .NET SDK version.
2. Restores local tools and NuGet packages.
3. Builds the solution.
4. Generates a JWT signing key in User Secrets if needed.
5. Creates the initial database migration when missing.
6. Runs the tests.
7. Prints the next steps.

The script can be run again without intentionally repeating completed setup steps.

### Configure SQL Server on macOS or Linux

Set your SQL Server connection string:

```bash
dotnet user-secrets set \
  "ConnectionStrings:DefaultConnection" \
  "<your-connection-string>" \
  --project src/StarterKit.Api
```

Replace `<your-connection-string>` with your actual connection string.

### Start the API

```bash
dotnet run --project src/StarterKit.Api --launch-profile http
```

Open the API documentation:

**http://localhost:5080/swagger**

---

## 3. Project Structure

You don't need to understand every file before getting started.

```text
StarterKit/
├── src/
│   └── StarterKit.Api/
│       ├── Auth/          # Registration, login, tokens
│       ├── Common/        # Validation, errors, security, pagination
│       ├── Domain/        # Core entities and user models
│       ├── Persistence/   # Database, configurations, migrations
│       └── Features/      # Add your application features here
│
├── tests/
│   ├── StarterKit.UnitTests/
│   └── StarterKit.IntegrationTests/
│
├── docs/                  # Guides and package decisions
├── setup.sh               # macOS / Linux setup
├── setup.ps1              # Windows PowerShell setup
├── setup.cmd              # Windows CMD setup
└── StarterKit.slnx
```

### Where should your code go?

- **Features/** — products, orders, profiles, and other business features.
- **Domain/** — entities and core business models.
- **Persistence/** — database configuration and migrations.
- **Common/** — reusable infrastructure shared across features.
- **Auth/** — authentication and token management.
- **tests/** — automated tests.

---

## 4. Add Your First Feature

For example, let's add a `Products` API.

The feature will support:

- Creating a product.
- Listing products with pagination.
- Deleting a product.

### Step 1: Create the entity

Add a `Product.cs` file under `Domain/`:

```csharp
public class Product : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
}
```

### Step 2: Register the entity in the database

Add the following property to `AppDbContext`:

```csharp
public DbSet<Product> Products => Set<Product>();
```

Create a configuration for the entity and define its database constraints.

### Step 3: Create a migration

```bash
dotnet ef migrations add AddProducts \
  --project src/StarterKit.Api \
  --output-dir Persistence/Migrations
```

Ensure the correct Development connection string is configured before applying migrations or running the application.

### Step 4: Create the API endpoints

Create a feature folder:

```text
Features/
└── Products/
    ├── ProductEndpoints.cs
    ├── CreateProductRequest.cs
    ├── CreateProductRequestValidator.cs
    └── ProductResponse.cs
```

Implement your endpoints, request validation, and response models.

The starter kit discovers endpoint implementations and validators automatically, so you shouldn't need to modify `Program.cs` for a normal feature.

For a complete working example, see [`docs/ADDING_FEATURES.md`](docs/ADDING_FEATURES.md).

---

## 5. Authentication Endpoints

The template includes these authentication endpoints:

| Method | Endpoint | Purpose |
|---|---|---|
| POST | `/api/auth/register` | Create an account |
| POST | `/api/auth/login` | Sign in and receive tokens |
| POST | `/api/auth/refresh` | Refresh tokens |
| POST | `/api/auth/logout` | Revoke a refresh token |
| GET | `/api/auth/me` | Get the current user |

Most endpoints require authentication by default. Registration, login, refresh, logout, and health checks are configured as exceptions.

Use `requests.http` to test the authentication flow.

**Note:** Swagger's Authorize button is not configured for the current authentication flow. See `docs/package-decisions.md` for details.

---

## 6. Security Basics

Before deploying the API:

- **JWT key:** Configure a strong signing key using environment variables or a secret store. Never commit secrets.
- **Database:** Use a production-ready SQL Server connection string and restrict database access.
- **CORS:** Add only the frontend origins that should access your API.
- **Authorization:** Apply the correct authorization policies to protected endpoints.
- **Admin account:** Configure the optional admin seed credentials securely.
- **Database migrations:** Run production migrations through your deployment process rather than automatically at application startup.
- **Reverse proxy:** Configure trusted forwarded headers if deploying behind a proxy or load balancer.
- **Rate limiting:** Review the limits for general requests and authentication endpoints.

See [`docs/SECURITY.md`](docs/SECURITY.md) for the full configuration and deployment checklist.

---

## 7. Configuration

The application uses configuration settings to control its behavior.

| Setting | Purpose |
|---|---|
| `ConnectionStrings:DefaultConnection` | SQL Server connection |
| `Jwt:Key` | JWT signing key |
| `Jwt:Issuer` | Token issuer |
| `Jwt:Audience` | Intended token audience |
| `Jwt:AccessTokenMinutes` | Access token lifetime |
| `Jwt:RefreshTokenDays` | Refresh token lifetime |
| `Cors:AllowedOrigins` | Allowed frontend origins |
| `RateLimiting:*` | Request rate limits |
| `Database:MigrateOnStartup` | Apply migrations on startup |
| `Database:SeedOnStartup` | Seed roles and optional admin |
| `Seed:AdminEmail` | Optional admin email |
| `Seed:AdminPassword` | Optional admin password |

For exact defaults, environment variable names, and examples, see [`docs/CONFIGURATION.md`](docs/CONFIGURATION.md).

---

## 8. Run Tests and Check Dependencies

Restore packages:

```bash
dotnet restore StarterKit.slnx
```

Build the solution:

```bash
dotnet build StarterKit.slnx --no-restore
```

Run tests:

```bash
dotnet test StarterKit.slnx --no-build
```

Check vulnerable packages:

```bash
dotnet list StarterKit.slnx package --vulnerable --include-transitive
```

Check for package updates:

```bash
dotnet list StarterKit.slnx package --outdated
```

The integration tests use in-memory SQLite, which does not reproduce every SQL Server behavior. Add SQL Server-backed integration tests when your features depend on SQL Server-specific behavior.

---

## 9. Documentation

| Document | What you'll find |
|---|---|
| `docs/GETTING_STARTED.md` | Detailed installation and setup |
| `docs/ADDING_FEATURES.md` | Complete feature implementation example |
| `docs/SECURITY.md` | Security configuration and deployment checklist |
| `docs/CONFIGURATION.md` | All settings, defaults, and environment variables |
| `docs/package-decisions.md` | NuGet package choices and rationale |

---

## Before Using This Template in Production

This template provides a starting point, not a guarantee of production readiness.

Build the solution, run the tests, verify the database configuration, review authentication and authorization, and complete the deployment security checklist before using it in a real application.
