<#
.SYNOPSIS
  One-command bootstrap for StarterKit: restore, build, secrets, first migration, tests.

.PARAMETER SkipTests
  Skip the test step.

.PARAMETER Run
  Start the API when setup finishes.

.EXAMPLE
  .\setup.ps1
  .\setup.ps1 -SkipTests -Run
#>
[CmdletBinding()]
param(
    [switch]$SkipTests,
    [switch]$Run
)

$ErrorActionPreference = 'Stop'
Set-Location -Path $PSScriptRoot

$Solution = 'StarterKit.slnx'
$ApiProject = 'src/StarterKit.Api'

function Write-Step {
    param([string]$Number, [string]$Message)
    Write-Host ''
    Write-Host "[$Number/8] $Message" -ForegroundColor Cyan
}

function Invoke-Dotnet {
    & dotnet @args
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($args -join ' ') failed with exit code $LASTEXITCODE."
    }
}

# 1. .NET SDK >= 10 ---------------------------------------------------------------------------
Write-Step 1 'Checking the .NET SDK (>= 10)'
$sdkVersion = $null
try { $sdkVersion = (& dotnet --version 2>$null) } catch { }
if (-not $sdkVersion) {
    throw 'The .NET SDK was not found (or global.json cannot be satisfied). Install .NET 10 SDK from https://dotnet.microsoft.com/download'
}
$sdkMajor = [int]($sdkVersion.Trim().Split('.')[0])
if ($sdkMajor -lt 10) {
    throw ".NET SDK $sdkVersion found, but version 10 or later is required."
}
Write-Host "Found .NET SDK $sdkVersion"

# 2. Local tools (dotnet-ef) ------------------------------------------------------------------
Write-Step 2 'Restoring local dotnet tools'
Invoke-Dotnet tool restore

# 3. Restore ----------------------------------------------------------------------------------
Write-Step 3 'Restoring NuGet packages'
Invoke-Dotnet restore $Solution

# 4. Build ------------------------------------------------------------------------------------
Write-Step 4 'Building'
Invoke-Dotnet build $Solution --no-restore

# 5. Jwt:Key in user-secrets ------------------------------------------------------------------
Write-Step 5 'Ensuring a JWT signing key exists in user-secrets'
$existing = & dotnet user-secrets list --project $ApiProject 2>$null
if ($existing -match '^Jwt:Key\s*=') {
    Write-Host 'Jwt:Key already present, leaving it untouched.'
}
else {
    $bytes = New-Object byte[] 64
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try { $rng.GetBytes($bytes) } finally { $rng.Dispose() }
    $key = [Convert]::ToBase64String($bytes)
    Invoke-Dotnet user-secrets set 'Jwt:Key' $key --project $ApiProject | Out-Null
    Write-Host 'Generated a random 64-byte Jwt:Key and stored it in user-secrets.'
}

# 6. First migration --------------------------------------------------------------------------
Write-Step 6 'Creating the InitialCreate migration (if missing)'
$migrationsDir = Join-Path $ApiProject 'Persistence/Migrations'
if (Test-Path $migrationsDir) {
    Write-Host 'Migrations folder already exists, skipping.'
}
else {
    $previousEnv = $env:ASPNETCORE_ENVIRONMENT
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    try {
        Invoke-Dotnet ef migrations add InitialCreate --project $ApiProject --output-dir Persistence/Migrations --no-build
    }
    finally {
        $env:ASPNETCORE_ENVIRONMENT = $previousEnv
    }
    Write-Host 'Rebuilding to include the new migration'
    Invoke-Dotnet build $Solution --no-restore
}

# 7. Tests ------------------------------------------------------------------------------------
if ($SkipTests) {
    Write-Step 7 'Skipping tests (-SkipTests)'
}
else {
    Write-Step 7 'Running tests'
    Invoke-Dotnet test $Solution --no-build
}

# 8. Next steps -------------------------------------------------------------------------------
Write-Step 8 'Done'
Write-Host ''
Write-Host 'Next steps:' -ForegroundColor Green
Write-Host '  1. Run the API:      dotnet run --project src/StarterKit.Api --launch-profile http'
Write-Host '  2. Open Swagger:     http://localhost:5080/swagger'
Write-Host '  3. Try auth calls:   requests.http (VS Code REST Client / Rider / Visual Studio)'
Write-Host '  4. Add a feature:    see "Adding a feature" in README.md'
Write-Host ''
Write-Host 'Development uses SQL Server LocalDB (ConnectionStrings:DefaultConnection in appsettings.Development.json).'

if ($Run) {
    Write-Host ''
    Write-Host 'Starting the API (Ctrl+C to stop)...' -ForegroundColor Green
    Invoke-Dotnet run --project $ApiProject --no-build --launch-profile http
}
