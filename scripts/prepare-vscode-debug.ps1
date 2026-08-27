[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$Project,

    [int]$Port = 0,

    [string]$ProcessName
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repositoryRoot $Project
$dockerEnvironmentFile = Join-Path $repositoryRoot "deploy/docker/.env"
$composeFile = Join-Path $repositoryRoot "deploy/docker/compose.infrastructure.yml"

if (-not (Test-Path -LiteralPath $projectPath -PathType Leaf)) {
    throw "Debug project does not exist: $projectPath"
}

if ($Port -gt 0) {
    $listeners = @(Get-NetTCPConnection -State Listen -LocalPort $Port -ErrorAction SilentlyContinue)
    if ($listeners.Count -gt 0) {
        $owners = $listeners |
            Select-Object -ExpandProperty OwningProcess -Unique |
            ForEach-Object {
                $process = Get-Process -Id $_ -ErrorAction SilentlyContinue
                if ($null -eq $process) { "PID $_" } else { "$($process.ProcessName) (PID $($_))" }
            }

        throw "Port $Port is already in use by $($owners -join ', '). Stop that service before launching it again, or select 'GRD: Attach to a running .NET service' in VS Code."
    }
}

if (-not [string]::IsNullOrWhiteSpace($ProcessName)) {
    $runningProcesses = @(Get-Process -Name $ProcessName -ErrorAction SilentlyContinue)
    if ($runningProcesses.Count -gt 0) {
        $processIds = ($runningProcesses.Id | Sort-Object -Unique) -join ", "
        throw "$ProcessName is already running (PID: $processIds). Stop it before rebuilding, or attach the VS Code debugger to the running process."
    }
}

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    throw "Docker was not found. Start Docker Desktop before debugging services that use MySQL or RabbitMQ."
}

$composeArguments = @("compose")
if (Test-Path -LiteralPath $dockerEnvironmentFile -PathType Leaf) {
    $composeArguments += @("--env-file", $dockerEnvironmentFile)
}
$composeArguments += @("-f", $composeFile, "up", "-d")

Write-Host "Ensuring local MySQL and RabbitMQ containers are running..." -ForegroundColor Cyan
& docker @composeArguments
if ($LASTEXITCODE -ne 0) {
    throw "Docker infrastructure failed to start."
}

Write-Host "Building debug target: $Project" -ForegroundColor Cyan
& dotnet build $projectPath --nologo
if ($LASTEXITCODE -ne 0) {
    throw "Debug build failed: $projectPath"
}

Write-Host "Debug target is ready." -ForegroundColor Green
