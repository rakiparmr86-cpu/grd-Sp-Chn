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

$conflicts = [System.Collections.Generic.List[string]]::new()
foreach ($entry in $servicePorts.GetEnumerator()) {
    $listeners = @(Get-NetTCPConnection -State Listen -LocalPort $entry.Value -ErrorAction SilentlyContinue)
    foreach ($listener in $listeners) {
        $process = Get-Process -Id $listener.OwningProcess -ErrorAction SilentlyContinue
        $owner = if ($null -eq $process) {
            "PID $($listener.OwningProcess)"
        }
        else {
            "$($process.ProcessName) (PID $($listener.OwningProcess))"
        }
        $conflicts.Add("$($entry.Key) port $($entry.Value): $owner")
    }
}

foreach ($processName in $workerProcessNames) {
    foreach ($process in @(Get-Process -Name $processName -ErrorAction SilentlyContinue)) {
        $conflicts.Add("$processName worker: PID $($process.Id)")
    }
}

if ($conflicts.Count -gt 0) {
    $details = $conflicts -join [Environment]::NewLine
    throw "One or more GRD services are already running. Stop them before using the all-services debug profile, or attach to them instead:$([Environment]::NewLine)$details"
}

Write-Host "Preparing every enabled GRD service for multi-target debugging..." -ForegroundColor Cyan
& (Join-Path $PSScriptRoot "start-local-services.ps1") -BuildOnly
if ($LASTEXITCODE -ne 0) {
    throw "The all-services debug preparation failed."
}

Write-Host "All debug targets are built and ready." -ForegroundColor Green
