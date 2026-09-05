[CmdletBinding()]
param(
    [string]$Database,
    [string]$User,
    [string]$Password
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$composeFile = Join-Path $repositoryRoot "deploy/docker/compose.infrastructure.yml"
$dockerEnvironmentFile = Join-Path $repositoryRoot "deploy/docker/.env"

$dockerEnvironment = @{}
if (Test-Path -LiteralPath $dockerEnvironmentFile -PathType Leaf) {
    foreach ($line in Get-Content -LiteralPath $dockerEnvironmentFile) {
        if ($line -notmatch '^\s*([^#=]+)=(.*)$') {
            continue
        }

        $name = $matches[1].Trim()
        $value = $matches[2].Trim()
        if (($value.StartsWith('"') -and $value.EndsWith('"')) -or
            ($value.StartsWith("'") -and $value.EndsWith("'"))) {
            $value = $value.Substring(1, $value.Length - 2)
        }
        $dockerEnvironment[$name] = $value
    }
}

if ([string]::IsNullOrWhiteSpace($Database)) {
    $Database = if ($dockerEnvironment.ContainsKey("MYSQL_DATABASE")) {
        $dockerEnvironment["MYSQL_DATABASE"]
    } else { "grd_local" }
}
if ([string]::IsNullOrWhiteSpace($User)) {
    $User = if ($dockerEnvironment.ContainsKey("MYSQL_USER")) {
        $dockerEnvironment["MYSQL_USER"]
    } else { "grd" }
}
if ([string]::IsNullOrWhiteSpace($Password)) {
    $Password = if ($dockerEnvironment.ContainsKey("MYSQL_PASSWORD")) {
        $dockerEnvironment["MYSQL_PASSWORD"]
    } else { "grd-local" }
}
$migrationFiles = @(
    (Join-Path $repositoryRoot "deploy/docker/mysql/init/003_identity_user_management.sql")
    (Join-Path $repositoryRoot "deploy/docker/mysql/init/004_identity_access_profiles.sql")
    (Join-Path $repositoryRoot "deploy/docker/mysql/init/005_identity_dynamic_permissions.sql")
    (Join-Path $repositoryRoot "deploy/docker/mysql/init/006_requisition_tracking_notifications.sql")
    (Join-Path $repositoryRoot "deploy/docker/mysql/init/007_supplier_master.sql")
    (Join-Path $repositoryRoot "deploy/docker/mysql/init/008_product_catalog_master.sql")
    (Join-Path $repositoryRoot "deploy/docker/mysql/init/009_purchase_order_vendor_dispatch.sql")
    (Join-Path $repositoryRoot "deploy/docker/mysql/init/010_quality_release_to_inventory.sql")
)
$composeArguments = @("compose")
if (Test-Path -LiteralPath $dockerEnvironmentFile -PathType Leaf) {
    $composeArguments += @("--env-file", $dockerEnvironmentFile)
}
$composeArguments += @("-f", $composeFile, "ps", "-q", "mysql")
$containerId = & docker @composeArguments

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

Write-Host "Local Identity, procurement, quality release, inventory, notifications, and master data are ready." -ForegroundColor Green
