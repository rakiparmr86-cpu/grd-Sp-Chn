[CmdletBinding()]
param(
    [string]$Service,
    [switch]$NoBuild,
    [switch]$NoRestore,
    [switch]$BuildOnly,
    [switch]$List,
    [switch]$SkipInfrastructure
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$startInfrastructureByDefault = $true

# Set Enabled to $false (recommended), or comment out the complete row, when a
# service is not needed locally. The VS Code compound task reads the same registry.
$serviceRegistry = @(
    [pscustomobject]@{ Name = "ApiGateway";       Enabled = $true; Port = 7000; Kind = "API Gateway"; Project = "src/backend/ApiGateway/GRD.SpChn.ApiGateway/GRD.SpChn.ApiGateway.csproj" }
    [pscustomobject]@{ Name = "Identity";         Enabled = $true; Port = 7001; Kind = "API"; Project = "src/backend/Services/Identity/GRD.SpChn.Identity.Api/GRD.SpChn.Identity.Api.csproj" }
    [pscustomobject]@{ Name = "Notifications";    Enabled = $true; Port = 7002; Kind = "API"; Project = "src/backend/Services/Notifications/GRD.SpChn.Notifications.Api/GRD.SpChn.Notifications.Api.csproj" }
    [pscustomobject]@{ Name = "OrderManagement";  Enabled = $true; Port = 5255; Kind = "API"; Project = "src/backend/Services/OrderManagement/GRD.SpChn.OrderManagement.Api/GRD.SpChn.OrderManagement.Api.csproj" }
    [pscustomobject]@{ Name = "Inventory";        Enabled = $true; Port = 5018; Kind = "API"; Project = "src/backend/Services/Inventory/GRD.SpChn.Inventory.Api/GRD.SpChn.Inventory.Api.csproj" }
    [pscustomobject]@{ Name = "Delivery";         Enabled = $true; Port = 5294; Kind = "API"; Project = "src/backend/Services/Delivery/GRD.SpChn.Delivery.Api/GRD.SpChn.Delivery.Api.csproj" }
    [pscustomobject]@{ Name = "Organization";     Enabled = $true; Port = 5218; Kind = "API"; Project = "src/backend/Services/Organization/GRD.SpChn.Organization.Api/GRD.SpChn.Organization.Api.csproj" }
    [pscustomobject]@{ Name = "Procurement";      Enabled = $true; Port = 5112; Kind = "API"; Project = "src/backend/Services/Procurement/GRD.SpChn.Procurement.Api/GRD.SpChn.Procurement.Api.csproj" }
    [pscustomobject]@{ Name = "ProductCatalog";   Enabled = $true; Port = 5006; Kind = "API"; Project = "src/backend/Services/ProductCatalog/GRD.SpChn.ProductCatalog.Api/GRD.SpChn.ProductCatalog.Api.csproj" }
    [pscustomobject]@{ Name = "Reporting";        Enabled = $true; Port = 5274; Kind = "API"; Project = "src/backend/Services/Reporting/GRD.SpChn.Reporting.Api/GRD.SpChn.Reporting.Api.csproj" }
    [pscustomobject]@{ Name = "Shipment";         Enabled = $true; Port = 5059; Kind = "API"; Project = "src/backend/Services/Shipment/GRD.SpChn.Shipment.Api/GRD.SpChn.Shipment.Api.csproj" }
    [pscustomobject]@{ Name = "Supplier";         Enabled = $true; Port = 5141; Kind = "API"; Project = "src/backend/Services/Supplier/GRD.SpChn.Supplier.Api/GRD.SpChn.Supplier.Api.csproj" }
    [pscustomobject]@{ Name = "Transportation";   Enabled = $true; Port = 5258; Kind = "API"; Project = "src/backend/Services/Transportation/GRD.SpChn.Transportation.Api/GRD.SpChn.Transportation.Api.csproj" }
    [pscustomobject]@{ Name = "Warehouse";        Enabled = $true; Port = 5276; Kind = "API"; Project = "src/backend/Services/Warehouse/GRD.SpChn.Warehouse.Api/GRD.SpChn.Warehouse.Api.csproj" }
    [pscustomobject]@{ Name = "OutboxPublisher";  Enabled = $true; Port = $null; Kind = "Worker"; Project = "src/backend/Workers/GRD.SpChn.OutboxPublisher/GRD.SpChn.OutboxPublisher.csproj" }
    [pscustomobject]@{ Name = "EventProcessor";   Enabled = $true; Port = $null; Kind = "Worker"; Project = "src/backend/Workers/GRD.SpChn.EventProcessor/GRD.SpChn.EventProcessor.csproj" }
    [pscustomobject]@{ Name = "ProjectionBuilder"; Enabled = $true; Port = $null; Kind = "Worker"; Project = "src/backend/Workers/GRD.SpChn.ProjectionBuilder/GRD.SpChn.ProjectionBuilder.csproj" }
)

$repositoryRoot = Split-Path -Parent $PSScriptRoot

function Set-DefaultEnvironmentVariable {
    param(
        [Parameter(Mandatory)]
        [string]$Name,

        [Parameter(Mandatory)]
        [string]$Value
    )

    if ([string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($Name))) {
        [Environment]::SetEnvironmentVariable($Name, $Value)
    }
}

function Initialize-LocalEnvironment {
    Set-DefaultEnvironmentVariable "ASPNETCORE_ENVIRONMENT" "Development"
    Set-DefaultEnvironmentVariable "DOTNET_ENVIRONMENT" "Development"
    Set-DefaultEnvironmentVariable "ConnectionStrings__Database" "Server=localhost;Port=3306;Database=grd_local;User ID=grd;Password=grd-local"
    Set-DefaultEnvironmentVariable "ConnectionStrings__OrderDatabase" "Server=localhost;Port=3306;Database=grd_local;User ID=grd;Password=grd-local"
    Set-DefaultEnvironmentVariable "ConnectionStrings__InventoryDatabase" "Server=localhost;Port=3306;Database=grd_local;User ID=grd;Password=grd-local"
    Set-DefaultEnvironmentVariable "RabbitMq__HostName" "localhost"
    Set-DefaultEnvironmentVariable "RabbitMq__Port" "5672"
    Set-DefaultEnvironmentVariable "RabbitMq__UserName" "grd"
    Set-DefaultEnvironmentVariable "RabbitMq__Password" "grd-local"
}

function Get-EnabledServices {
    return @($serviceRegistry | Where-Object { $_.Enabled })
}

function Assert-ServiceRegistry {
    $duplicateNames = $serviceRegistry |
        Group-Object -Property { $_.Name } |
        Where-Object { $_.Count -gt 1 }
    if ($duplicateNames) {
        throw "Duplicate service name(s): $($duplicateNames.Name -join ', ')."
    }

    $duplicatePorts = $serviceRegistry |
        Where-Object { $_.Enabled -and $null -ne $_.Port } |
        Group-Object -Property { $_.Port } |
        Where-Object { $_.Count -gt 1 }
    if ($duplicatePorts) {
        throw "Duplicate enabled port(s): $($duplicatePorts.Name -join ', ')."
    }

    foreach ($entry in $serviceRegistry) {
        $projectPath = Join-Path $repositoryRoot $entry.Project
        if (-not (Test-Path -LiteralPath $projectPath -PathType Leaf)) {
            throw "Project for '$($entry.Name)' does not exist: $projectPath"
        }
    }
}

function Show-ServiceRegistry {
    $serviceRegistry |
        Select-Object Name, Kind, Enabled, @{ Name = "Address"; Expression = {
            if ($null -eq $_.Port) { "background worker" }
            else { "http://localhost:$($_.Port)" }
        } } |
        Format-Table -AutoSize
}

function Start-LocalInfrastructure {
    if ($SkipInfrastructure -or -not $startInfrastructureByDefault) {
        Write-Host "Skipping Docker infrastructure by request." -ForegroundColor Yellow
        return
    }

    if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
        throw "Docker was not found. Install/start Docker Desktop or use -SkipInfrastructure."
    }

    Write-Host "Starting MySQL and RabbitMQ containers..." -ForegroundColor Cyan
    & docker compose `
        -f (Join-Path $repositoryRoot "deploy/docker/compose.infrastructure.yml") `
        up -d
    if ($LASTEXITCODE -ne 0) {
        throw "Docker infrastructure failed to start."
    }
}

function Build-EnabledServices {
    foreach ($entry in Get-EnabledServices) {
        $projectPath = Join-Path $repositoryRoot $entry.Project
        Write-Host "Building $($entry.Name)..." -ForegroundColor Cyan
        $buildArguments = @("build", $projectPath, "--nologo")
        if ($NoRestore) {
            $buildArguments += "--no-restore"
        }

        & dotnet @buildArguments
        if ($LASTEXITCODE -ne 0) {
            throw "Build failed for '$($entry.Name)'. No services were launched."
        }
    }
}

function Invoke-Service {
    param(
        [Parameter(Mandatory)]
        [object]$Entry
    )

    if (-not $Entry.Enabled) {
        Write-Host "$($Entry.Name) is disabled; nothing was started." -ForegroundColor Yellow
        return
    }

    Initialize-LocalEnvironment
    Set-Location -LiteralPath $repositoryRoot
    $projectPath = Join-Path $repositoryRoot $Entry.Project

    if (-not $NoBuild) {
        Write-Host "Building $($Entry.Name) before launch..." -ForegroundColor Cyan
        $buildArguments = @("build", $projectPath, "--nologo")
        if ($NoRestore) {
            $buildArguments += "--no-restore"
        }

        & dotnet @buildArguments
        if ($LASTEXITCODE -ne 0) {
            throw "Build failed for '$($Entry.Name)'."
        }
    }

    $address = if ($null -eq $Entry.Port) {
        "background worker"
    }
    else {
        "http://localhost:$($Entry.Port)"
    }

    Write-Host "Starting $($Entry.Name) ($($Entry.Kind)) - $address" -ForegroundColor Green
    & dotnet run --project $projectPath --no-build
    exit $LASTEXITCODE
}

Assert-ServiceRegistry

if ($List) {
    Show-ServiceRegistry
    return
}

if (-not [string]::IsNullOrWhiteSpace($Service)) {
    $selectedService = $serviceRegistry |
        Where-Object { $_.Name -eq $Service } |
        Select-Object -First 1

    if ($null -eq $selectedService) {
        Write-Host "'$Service' is not configured (it may be commented out); skipping." -ForegroundColor Yellow
        return
    }

    Invoke-Service -Entry $selectedService
    return
}

Initialize-LocalEnvironment
Start-LocalInfrastructure

if (-not $NoBuild) {
    Build-EnabledServices
}

if ($BuildOnly) {
    Write-Host "Enabled services built successfully; no processes were launched." -ForegroundColor Green
    return
}

$enabledServices = Get-EnabledServices
Write-Host "Launching $($enabledServices.Count) enabled processes in separate consoles..." -ForegroundColor Cyan

$powerShellCommand = Get-Command pwsh -ErrorAction SilentlyContinue
if ($null -eq $powerShellCommand) {
    $powerShellCommand = Get-Command powershell.exe -ErrorAction Stop
}

foreach ($entry in $enabledServices) {
    $arguments = @(
        "-NoExit"
        "-NoProfile"
        "-File"
        $PSCommandPath
        "-Service"
        $entry.Name
        "-NoBuild"
        "-SkipInfrastructure"
    )

    $process = Start-Process `
        -FilePath $powerShellCommand.Source `
        -ArgumentList $arguments `
        -WorkingDirectory $repositoryRoot `
        -WindowStyle Normal `
        -PassThru

    Write-Host "  $($entry.Name): console process $($process.Id)" -ForegroundColor DarkGray
}

Write-Host "All enabled services were launched. Close a service console or press Ctrl+C in it to stop only that service." -ForegroundColor Green
