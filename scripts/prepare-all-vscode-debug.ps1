[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$servicePorts = [ordered]@{
    ApiGateway = 7000
    Identity = 7001
    Notifications = 7002
    OrderManagement = 5255
    Inventory = 5018
    Delivery = 5294
    Organization = 5218
    Procurement = 5112
    ProductCatalog = 5006
    Reporting = 5274
    Shipment = 5059
    Supplier = 5141
    Transportation = 5258
    Warehouse = 5276
}
$workerProcessNames = @(
    "GRD.SpChn.OutboxPublisher",
    "GRD.SpChn.EventProcessor",
    "GRD.SpChn.ProjectionBuilder"
)

. (Join-Path $PSScriptRoot "local-debug-processes.ps1")

Stop-GrdWorkspaceProcessesForDebug `
    -RepositoryRoot $repositoryRoot `
    -ServicePorts $servicePorts `
    -ProcessNames $workerProcessNames `
    -IncludeAllWorkspaceProcesses

Write-Host "Preparing every enabled GRD service for multi-target debugging..." -ForegroundColor Cyan
& (Join-Path $PSScriptRoot "start-local-services.ps1") -BuildOnly
if ($LASTEXITCODE -ne 0) {
    throw "The all-services debug preparation failed."
}

Write-Host "All debug targets are built and ready." -ForegroundColor Green
