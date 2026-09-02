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
    [pscustomobject]@{ Name = "Web";              Enabled = $true; Port = 5173; Kind = "React UI"; Runtime = "Node"; Project = "src/frontend/grd-spchn-web" }
)

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$dockerEnvironmentFile = Join-Path $repositoryRoot "deploy/docker/.env"

function Read-DotEnvFile {
    param([Parameter(Mandatory)][string]$Path)

    $values = @{}
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return $values
    }

    foreach ($line in Get-Content -LiteralPath $Path) {
        if ($line -notmatch '^\s*([^#=]+)=(.*)$') {
            continue
        }

        $name = $matches[1].Trim()
        $value = $matches[2].Trim()
        if (($value.StartsWith('"') -and $value.EndsWith('"')) -or
            ($value.StartsWith("'") -and $value.EndsWith("'"))) {
            $value = $value.Substring(1, $value.Length - 2)
        }

        $values[$name] = $value
    }

    return $values
}

$dockerEnvironment = Read-DotEnvFile -Path $dockerEnvironmentFile

function Set-DefaultEnvironmentVariable {
    param(
        [Parameter(Mandatory)]
        [string]$Name,

        [Parameter(Mandatory)]
        [AllowEmptyString()]
        [string]$Value
    )

    if ([string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($Name))) {
        [Environment]::SetEnvironmentVariable($Name, $Value)
    }
}

function Get-LocalInfrastructureSetting {
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][AllowEmptyString()][string]$DefaultValue
    )

    $processValue = [Environment]::GetEnvironmentVariable($Name)
    if (-not [string]::IsNullOrWhiteSpace($processValue)) {
        return $processValue
    }

    if ($dockerEnvironment.ContainsKey($Name) -and
        -not [string]::IsNullOrWhiteSpace($dockerEnvironment[$Name])) {
        return $dockerEnvironment[$Name]
    }

    return $DefaultValue
}

function Initialize-LocalEnvironment {
    $mysqlDatabase = Get-LocalInfrastructureSetting "MYSQL_DATABASE" "grd_local"
    $mysqlUser = Get-LocalInfrastructureSetting "MYSQL_USER" "grd"
    $mysqlPassword = Get-LocalInfrastructureSetting "MYSQL_PASSWORD" "grd-local"
    $mysqlPort = Get-LocalInfrastructureSetting "MYSQL_PORT" "3306"
    $rabbitMqUser = Get-LocalInfrastructureSetting "RABBITMQ_USER" "grd"
    $rabbitMqPassword = Get-LocalInfrastructureSetting "RABBITMQ_PASSWORD" "grd-local"
    $rabbitMqPort = Get-LocalInfrastructureSetting "RABBITMQ_PORT" "5672"
    $smtpEnabled = Get-LocalInfrastructureSetting "SMTP_ENABLED" "false"
    $smtpHost = Get-LocalInfrastructureSetting "SMTP_HOST" ""
    $smtpPort = Get-LocalInfrastructureSetting "SMTP_PORT" "587"
    $smtpEnableSsl = Get-LocalInfrastructureSetting "SMTP_ENABLE_SSL" "true"
    $smtpUserName = Get-LocalInfrastructureSetting "SMTP_USERNAME" ""
    $smtpPassword = Get-LocalInfrastructureSetting "SMTP_PASSWORD" ""
    $smtpFromAddress = Get-LocalInfrastructureSetting "SMTP_FROM_ADDRESS" "notifications@grd.local"
    $smtpFromName = Get-LocalInfrastructureSetting "SMTP_FROM_NAME" "GRD Supply Chain"
    $databaseConnection = "Server=localhost;Port=$mysqlPort;Database=$mysqlDatabase;User ID=$mysqlUser;Password=$mysqlPassword;SslMode=None"

    Set-DefaultEnvironmentVariable "ASPNETCORE_ENVIRONMENT" "Development"
    Set-DefaultEnvironmentVariable "DOTNET_ENVIRONMENT" "Development"
    Set-DefaultEnvironmentVariable "ConnectionStrings__Database" $databaseConnection
    Set-DefaultEnvironmentVariable "ConnectionStrings__OrderDatabase" $databaseConnection
    Set-DefaultEnvironmentVariable "ConnectionStrings__InventoryDatabase" $databaseConnection
    Set-DefaultEnvironmentVariable "ConnectionStrings__ProcurementDatabase" $databaseConnection
    Set-DefaultEnvironmentVariable "ConnectionStrings__WarehouseDatabase" $databaseConnection
    Set-DefaultEnvironmentVariable "RabbitMq__HostName" "localhost"
    Set-DefaultEnvironmentVariable "RabbitMq__Port" $rabbitMqPort
    Set-DefaultEnvironmentVariable "RabbitMq__UserName" $rabbitMqUser
    Set-DefaultEnvironmentVariable "RabbitMq__Password" $rabbitMqPassword
    Set-DefaultEnvironmentVariable "Smtp__Enabled" $smtpEnabled
    Set-DefaultEnvironmentVariable "Smtp__Host" $smtpHost
    Set-DefaultEnvironmentVariable "Smtp__Port" $smtpPort
    Set-DefaultEnvironmentVariable "Smtp__EnableSsl" $smtpEnableSsl
    Set-DefaultEnvironmentVariable "Smtp__UserName" $smtpUserName
    Set-DefaultEnvironmentVariable "Smtp__Password" $smtpPassword
    Set-DefaultEnvironmentVariable "Smtp__FromAddress" $smtpFromAddress
    Set-DefaultEnvironmentVariable "Smtp__FromName" $smtpFromName
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
        $isNode = $entry.PSObject.Properties.Name -contains "Runtime" -and $entry.Runtime -eq "Node"
        $pathType = if ($isNode) { "Container" } else { "Leaf" }
        if (-not (Test-Path -LiteralPath $projectPath -PathType $pathType)) {
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
    $composeArguments = @("compose")
    if (Test-Path -LiteralPath $dockerEnvironmentFile -PathType Leaf) {
        $composeArguments += @("--env-file", $dockerEnvironmentFile)
    }
    $composeArguments += @(
        "-f",
        (Join-Path $repositoryRoot "deploy/docker/compose.infrastructure.yml"),
        "up",
        "-d",
        "--wait")
    & docker @composeArguments
    if ($LASTEXITCODE -ne 0) {
        throw "Docker infrastructure failed to start."
    }

    Write-Host "Applying idempotent local database migrations..." -ForegroundColor Cyan
    & (Join-Path $PSScriptRoot "apply-local-identity-seed.ps1")
    if ($LASTEXITCODE -ne 0) {
        throw "Local database migrations failed."
    }
}

function Build-EnabledServices {
    foreach ($entry in Get-EnabledServices) {
        $projectPath = Join-Path $repositoryRoot $entry.Project
        Write-Host "Building $($entry.Name)..." -ForegroundColor Cyan
        $isNode = $entry.PSObject.Properties.Name -contains "Runtime" -and $entry.Runtime -eq "Node"
        if ($isNode) {
            Push-Location -LiteralPath $projectPath
            try {
                if (-not (Test-Path -LiteralPath "node_modules" -PathType Container)) {
                    Write-Host "Installing frontend packages (first run only)..." -ForegroundColor Cyan
                    & npm install
                    if ($LASTEXITCODE -ne 0) { throw "npm install failed for '$($entry.Name)'." }
                }
                & npm run build
                if ($LASTEXITCODE -ne 0) { throw "Build failed for '$($entry.Name)'." }
            }
            finally {
                Pop-Location
            }
            continue
        }

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
    $isNode = $Entry.PSObject.Properties.Name -contains "Runtime" -and $Entry.Runtime -eq "Node"

    if ($isNode) {
        Push-Location -LiteralPath $projectPath
        try {
            if (-not (Test-Path -LiteralPath "node_modules" -PathType Container)) {
                if ($NoBuild) {
                    throw "Frontend packages are missing. Run 'npm install' in '$projectPath' first."
                }

                Write-Host "Installing frontend packages (first run only)..." -ForegroundColor Cyan
                & npm install
                if ($LASTEXITCODE -ne 0) { throw "npm install failed for '$($Entry.Name)'." }
            }

            if (-not $NoBuild) {
                Write-Host "Building $($Entry.Name) before launch..." -ForegroundColor Cyan
                & npm run build
                if ($LASTEXITCODE -ne 0) { throw "Build failed for '$($Entry.Name)'." }
            }

            Write-Host "Starting $($Entry.Name) ($($Entry.Kind)) - http://localhost:$($Entry.Port)" -ForegroundColor Green
            & npm run dev
            exit $LASTEXITCODE
        }
        finally {
            Pop-Location
        }
    }

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
