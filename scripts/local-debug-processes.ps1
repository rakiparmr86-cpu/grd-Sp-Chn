Set-StrictMode -Version Latest

function Get-GrdProcessDescriptor {
    param(
        [Parameter(Mandatory)]
        [int]$ProcessId
    )

    $process = Get-Process -Id $ProcessId -ErrorAction SilentlyContinue
    if ($null -eq $process) {
        return [pscustomobject]@{
            Id = $ProcessId
            Name = "unknown"
            Path = $null
            CommandLine = $null
        }
    }

    $processPath = $null
    try { $processPath = $process.Path } catch { }

    $commandLine = $null
    try {
        $commandLine = (Get-CimInstance Win32_Process -Filter "ProcessId = $ProcessId" -ErrorAction Stop).CommandLine
    }
    catch {
        # Some restricted shells cannot read another process command line. The
        # executable path is still sufficient for GRD apphost processes.
    }

    return [pscustomobject]@{
        Id = $ProcessId
        Name = $process.ProcessName
        Path = $processPath
        CommandLine = $commandLine
    }
}

function Test-GrdWorkspaceProcess {
    param(
        [Parameter(Mandatory)]
        [object]$Process,

        [Parameter(Mandatory)]
        [string]$RepositoryRoot
    )

    $normalizedRoot = [System.IO.Path]::GetFullPath($RepositoryRoot).TrimEnd('\', '/')
    $hasGrdIdentity = $Process.Name -like "GRD.SpChn.*" -or
        (-not [string]::IsNullOrWhiteSpace($Process.CommandLine) -and
            $Process.CommandLine -like "*GRD.SpChn.*")
    if (-not $hasGrdIdentity) { return $false }

    foreach ($value in @($Process.Path, $Process.CommandLine)) {
        if (-not [string]::IsNullOrWhiteSpace($value) -and
            $value.IndexOf($normalizedRoot, [System.StringComparison]::OrdinalIgnoreCase) -ge 0) {
            return $true
        }
    }

    return $false
}

function Stop-GrdWorkspaceProcessesForDebug {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$RepositoryRoot,

        [System.Collections.IDictionary]$ServicePorts = @{},

        [string[]]$ProcessNames = @(),

        [switch]$IncludeAllWorkspaceProcesses
    )

    $ownedProcesses = @{}
    $unsafeConflicts = [System.Collections.Generic.List[string]]::new()

    foreach ($entry in $ServicePorts.GetEnumerator()) {
        $listeners = @(Get-NetTCPConnection -State Listen -LocalPort $entry.Value -ErrorAction SilentlyContinue)
        $listenerProcessIds = @($listeners |
            ForEach-Object { $_.OwningProcess } |
            Sort-Object -Unique)
        foreach ($processId in $listenerProcessIds) {
            $descriptor = Get-GrdProcessDescriptor -ProcessId $processId
            if (Test-GrdWorkspaceProcess -Process $descriptor -RepositoryRoot $RepositoryRoot) {
                $ownedProcesses[[string]$descriptor.Id] = $descriptor
            }
            else {
                $unsafeConflicts.Add(
                    "$($entry.Key) port $($entry.Value): $($descriptor.Name) (PID $($descriptor.Id))")
            }
        }
    }

    foreach ($processName in $ProcessNames) {
        foreach ($process in @(Get-Process -Name $processName -ErrorAction SilentlyContinue)) {
            $descriptor = Get-GrdProcessDescriptor -ProcessId $process.Id
            if (Test-GrdWorkspaceProcess -Process $descriptor -RepositoryRoot $RepositoryRoot) {
                $ownedProcesses[[string]$descriptor.Id] = $descriptor
            }
            else {
                $unsafeConflicts.Add("$processName process: PID $($descriptor.Id) outside this repository")
            }
        }
    }

    if ($IncludeAllWorkspaceProcesses) {
        foreach ($process in @(Get-Process -Name "GRD.SpChn.*" -ErrorAction SilentlyContinue)) {
            $descriptor = Get-GrdProcessDescriptor -ProcessId $process.Id
            if (Test-GrdWorkspaceProcess -Process $descriptor -RepositoryRoot $RepositoryRoot) {
                $ownedProcesses[[string]$descriptor.Id] = $descriptor
            }
        }

        try {
            $dotnetProcesses = @(Get-CimInstance Win32_Process -Filter "Name = 'dotnet.exe'" -ErrorAction Stop)
            foreach ($dotnetProcess in $dotnetProcesses) {
                $descriptor = [pscustomobject]@{
                    Id = [int]$dotnetProcess.ProcessId
                    Name = "dotnet"
                    Path = $dotnetProcess.ExecutablePath
                    CommandLine = $dotnetProcess.CommandLine
                }
                if (Test-GrdWorkspaceProcess -Process $descriptor -RepositoryRoot $RepositoryRoot) {
                    $ownedProcesses[[string]$descriptor.Id] = $descriptor
                }
            }
        }
        catch {
            Write-Host "Could not inspect all dotnet command lines; required ports remain protected." -ForegroundColor Yellow
        }
    }

    if ($unsafeConflicts.Count -gt 0) {
        $details = ($unsafeConflicts | Sort-Object -Unique) -join [Environment]::NewLine
        throw "A required GRD port/process is owned by another application or repository. It was not stopped:$([Environment]::NewLine)$details"
    }

    $processesToStop = @($ownedProcesses.Values | Sort-Object Id -Unique)
    if ($processesToStop.Count -eq 0) { return }

    Write-Host "Stopping $($processesToStop.Count) stale GRD process(es) owned by this repository..." -ForegroundColor Yellow
    foreach ($process in $processesToStop) {
        Write-Host "  $($process.Name) (PID $($process.Id))" -ForegroundColor DarkYellow
        Stop-Process -Id $process.Id -Force -ErrorAction Stop
    }

    $deadline = [DateTime]::UtcNow.AddSeconds(10)
    do {
        $stillRunning = @($processesToStop | Where-Object {
            $null -ne (Get-Process -Id $_.Id -ErrorAction SilentlyContinue)
        })
        if ($stillRunning.Count -eq 0) { break }
        Start-Sleep -Milliseconds 150
    } while ([DateTime]::UtcNow -lt $deadline)

    if ($stillRunning.Count -gt 0) {
        $remainingIds = ($stillRunning.Id | Sort-Object -Unique) -join ", "
        throw "GRD process(es) did not stop within 10 seconds (PID: $remainingIds)."
    }

    foreach ($entry in $ServicePorts.GetEnumerator()) {
        $remainingListeners = @(Get-NetTCPConnection -State Listen -LocalPort $entry.Value -ErrorAction SilentlyContinue)
        if ($remainingListeners.Count -gt 0) {
            $remainingIds = ($remainingListeners.OwningProcess | Sort-Object -Unique) -join ", "
            throw "$($entry.Key) port $($entry.Value) was not released (PID: $remainingIds)."
        }
    }
}
