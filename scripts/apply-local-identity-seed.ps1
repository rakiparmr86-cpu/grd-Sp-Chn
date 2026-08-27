[CmdletBinding()]
param(
    [string]$Database = "grd_local",
    [string]$User = "grd",
    [string]$Password = "grd-local"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$composeFile = Join-Path $repositoryRoot "deploy/docker/compose.infrastructure.yml"
$migrationFiles = @(
    (Join-Path $repositoryRoot "deploy/docker/mysql/init/003_identity_user_management.sql")
    (Join-Path $repositoryRoot "deploy/docker/mysql/init/004_identity_access_profiles.sql")
)
$containerId = & docker compose -f $composeFile ps -q mysql

if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($containerId)) {
    throw "The GRD MySQL container is not running. Start Docker infrastructure first."
}

foreach ($migrationFile in $migrationFiles) {
    Write-Host "Applying $(Split-Path -Leaf $migrationFile)..." -ForegroundColor Cyan
    Get-Content -LiteralPath $migrationFile -Raw |
        & docker exec -i $containerId mysql "-u$User" "-p$Password" $Database

    if ($LASTEXITCODE -ne 0) {
        throw "Identity migration failed: $migrationFile"
    }
}

Write-Host "Local Identity accounts and database-owned access profiles are ready." -ForegroundColor Green
