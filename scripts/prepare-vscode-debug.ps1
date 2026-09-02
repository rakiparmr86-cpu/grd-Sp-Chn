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

. (Join-Path $PSScriptRoot "local-debug-processes.ps1")

if (-not (Test-Path -LiteralPath $projectPath -PathType Leaf)) {
    throw "Debug project does not exist: $projectPath"
}

$targetPorts = @{}
if ($Port -gt 0) { $targetPorts["Debug target"] = $Port }
$targetProcessNames = @()
if (-not [string]::IsNullOrWhiteSpace($ProcessName)) {
    $targetProcessNames = @($ProcessName)
}

Stop-GrdWorkspaceProcessesForDebug `
    -RepositoryRoot $repositoryRoot `
    -ServicePorts $targetPorts `
    -ProcessNames $targetProcessNames

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
