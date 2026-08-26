# Running GRD services locally

## Recommended approach

Run services as separate processes. They may share MySQL, RabbitMQ, and common class
libraries, but every API/worker must own one running process and one terminal.

The repository provides two entry points backed by the same service registry:

1. VS Code task `GRD: Start enabled services` opens one dedicated integrated terminal
   per enabled service.
2. `scripts/start-local-services.ps1` builds once and opens one separate PowerShell
   console per enabled service.

Both approaches deliberately build enabled projects sequentially and then use
`dotnet run --no-build`. This prevents multiple parallel `dotnet run` commands from
trying to rebuild/copy the same shared outputs.

## Enable or disable a service

Open:

```text
scripts/start-local-services.ps1
```

Each process has one registry row:

```powershell
@{ Name = "Identity"; Enabled = $true; Port = 7001; ... }
```

Set `Enabled = $false` when it should not run:

```powershell
@{ Name = "Identity"; Enabled = $false; Port = 7001; ... }
```

Setting the flag is safer and clearer than commenting. Commenting out the complete
row is also supported, but never comment only part of a hashtable row.

Docker infrastructure is controlled separately near the top of the same file:

```powershell
$startInfrastructureByDefault = $true
```

Set it to `$false` if MySQL and RabbitMQ are already managed elsewhere.

## Start from VS Code

1. Open the repository root `D:\newdata\grd-Sp-Chn` in VS Code.
2. Select **Terminal -> Run Task**.
3. Select **GRD: Start enabled services**.

VS Code performs this sequence:

```text
Start MySQL + RabbitMQ
  -> build enabled projects one at a time
  -> launch all enabled projects in parallel with --no-build
  -> show each process in its own dedicated terminal
```

The task definition is in `.vscode/tasks.json`. Its `instanceLimit: 1` setting
prevents the same VS Code service task from being started twice.

To stop:

- focus one service terminal and press `Ctrl+C` to stop only that process; or
- use **Terminal -> Terminate Task** and terminate all GRD tasks.

After changing code, stop the affected task, run `GRD: Prepare enabled services`, and
start it again. Do not rebuild a project while the same project's executable is
running, because Windows can lock its generated `.exe`.

## Start from PowerShell

From the repository root:

```powershell
pwsh -NoProfile -File scripts\start-local-services.ps1
```

This starts Docker infrastructure, builds every enabled project sequentially, and
opens a separate visible console for each enabled service.

List configured services without starting anything:

```powershell
pwsh -NoProfile -File scripts\start-local-services.ps1 -List
```

Build enabled services without launching them:

```powershell
pwsh -NoProfile -File scripts\start-local-services.ps1 -BuildOnly
```

If the checkout has already been restored and NuGet feeds are temporarily
unavailable, skip restore during the build:

```powershell
pwsh -NoProfile -File scripts\start-local-services.ps1 `
  -BuildOnly `
  -NoRestore
```

Run only one service in the current console:

```powershell
pwsh -NoProfile -File scripts\start-local-services.ps1 -Service Identity
```

Skip Docker for one invocation:

```powershell
pwsh -NoProfile -File scripts\start-local-services.ps1 -SkipInfrastructure
```

Environment variables already defined in the calling terminal are preserved. The
script supplies local defaults only when a value is missing, including MySQL and
RabbitMQ settings. Override a value before launching when required, for example:

```powershell
$env:ConnectionStrings__Database =
  "Server=localhost;Port=3308;Database=grd_local;User ID=grd;Password=grd-local"

pwsh -NoProfile -File scripts\start-local-services.ps1
```

## Current service addresses

| Process | Address |
| --- | --- |
| API Gateway | `http://localhost:7000` |
| Identity | `http://localhost:7001` |
| Notifications | `http://localhost:7002` |
| Order Management | `http://localhost:5255` |
| Inventory | `http://localhost:5018` |
| Product Catalog | `http://localhost:5006` |
| Shipment | `http://localhost:5059` |
| Procurement | `http://localhost:5112` |
| Supplier | `http://localhost:5141` |
| Organization | `http://localhost:5218` |
| Transportation | `http://localhost:5258` |
| Reporting | `http://localhost:5274` |
| Warehouse | `http://localhost:5276` |
| Delivery | `http://localhost:5294` |

`OutboxPublisher`, `EventProcessor`, and `ProjectionBuilder` are background workers
and do not listen on HTTP ports.

## Why the previous process conflict happened

Different microservices can run together because they use different project output
folders and ports. This error means the same executable is already running:

```text
Could not copy ... GRD.SpChn.Identity.Api.exe because it is being used by another process
```

Typical cause:

```text
Identity is already running
  -> another dotnet run for Identity starts
  -> dotnet tries to rebuild Identity
  -> the existing Identity process locks its .exe
```

It is not an Identity-versus-Notifications conflict. Check the project name shown in
the locked path and stop that existing process/task before rebuilding it.
