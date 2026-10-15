#!/usr/bin/env bash
# One-command bootstrap for StarterKit: restore, build, secrets, first migration, tests.
# Usage: ./setup.sh [--skip-tests] [--run]
set -euo pipefail

cd "$(dirname "$0")"

SOLUTION="StarterKit.slnx"
API_PROJECT="src/StarterKit.Api"
SKIP_TESTS=0
RUN=0

for arg in "$@"; do
  case "$arg" in
    --skip-tests) SKIP_TESTS=1 ;;
    --run) RUN=1 ;;
    -h|--help)
      echo "Usage: ./setup.sh [--skip-tests] [--run]"
      exit 0
      ;;
    *)
      echo "Unknown option: $arg" >&2
      exit 1
      ;;
  esac
done

step() {
  echo
  echo "[$1/8] $2"
}

# 1. .NET SDK >= 10
step 1 "Checking the .NET SDK (>= 10)"
if ! command -v dotnet >/dev/null 2>&1; then
  echo "The .NET SDK was not found. Install .NET 10 SDK from https://dotnet.microsoft.com/download" >&2
  exit 1
fi
SDK_VERSION="$(dotnet --version 2>/dev/null || true)"
if [ -z "$SDK_VERSION" ]; then
  echo "dotnet could not resolve an SDK that satisfies global.json (needs 10.0.100 or later)." >&2
  exit 1
fi
SDK_MAJOR="${SDK_VERSION%%.*}"
if [ "$SDK_MAJOR" -lt 10 ]; then
  echo ".NET SDK $SDK_VERSION found, but version 10 or later is required." >&2
  exit 1
fi
echo "Found .NET SDK $SDK_VERSION"

# 2. Local tools (dotnet-ef)
step 2 "Restoring local dotnet tools"
dotnet tool restore

# 3. Restore
step 3 "Restoring NuGet packages"
dotnet restore "$SOLUTION"

# 4. Build
step 4 "Building"
dotnet build "$SOLUTION" --no-restore

# 5. Jwt:Key in user-secrets
step 5 "Ensuring a JWT signing key exists in user-secrets"
if dotnet user-secrets list --project "$API_PROJECT" 2>/dev/null | grep -q '^Jwt:Key'; then
  echo "Jwt:Key already present, leaving it untouched."
else
  KEY="$(head -c 64 /dev/urandom | base64 | tr -d '\n=')"
  dotnet user-secrets set "Jwt:Key" "$KEY" --project "$API_PROJECT" >/dev/null
  echo "Generated a random 64-byte Jwt:Key and stored it in user-secrets."
fi

# 6. First migration
step 6 "Creating the InitialCreate migration (if missing)"
if [ -d "$API_PROJECT/Persistence/Migrations" ]; then
  echo "Migrations folder already exists, skipping."
else
  ASPNETCORE_ENVIRONMENT=Development dotnet ef migrations add InitialCreate \
    --project "$API_PROJECT" --output-dir Persistence/Migrations --no-build
  echo "Rebuilding to include the new migration"
  dotnet build "$SOLUTION" --no-restore
fi

# 7. Tests
if [ "$SKIP_TESTS" -eq 1 ]; then
  step 7 "Skipping tests (--skip-tests)"
else
  step 7 "Running tests"
  dotnet test "$SOLUTION" --no-build
fi

# 8. Next steps
step 8 "Done"
cat <<'MSG'

Next steps:
  1. Run the API:      dotnet run --project src/StarterKit.Api --launch-profile http
  2. Open Swagger:     http://localhost:5080/swagger
  3. Try auth calls:   requests.http (VS Code REST Client / Rider / Visual Studio)
  4. Add a feature:    see "Adding a feature" in README.md

Development uses SQL Server LocalDB on Windows (ConnectionStrings:DefaultConnection in
appsettings.Development.json). On macOS/Linux point the connection string at a SQL Server container.
MSG

if [ "$RUN" -eq 1 ]; then
  echo
  echo "Starting the API (Ctrl+C to stop)..."
  dotnet run --project "$API_PROJECT" --no-build --launch-profile http
fi
