# Understanding the GRD VS Code F5 startup flow

This document explains exactly what happens when the VS Code profile
`GRD: Start ALL backend services with debugger` is selected and **F5** is pressed.
It also separates concepts that are easy to confuse: containers, .NET processes,
PowerShell scripts, SQL initialization, Outbox processing, and smoke tests.

## 1. The short mental model

```text
F5
  -> VS Code reads .vscode/launch.json
  -> the compound profile requests one pre-launch task
  -> .vscode/tasks.json starts PowerShell
  -> prepare-all-vscode-debug.ps1 safely stops stale processes from this repository
  -> start-local-services.ps1 -BuildOnly starts Docker and builds projects
  -> Docker Compose starts/reuses MySQL and RabbitMQ
  -> the pre-launch task finishes
  -> VS Code launches 14 API processes and 3 worker processes
  -> the C# debugger is attached to every launched backend process
```

The React application is not launched by this compound debug profile. Start React
separately when it is needed.

## 2. Files involved when F5 is pressed

| Order | File | Responsibility |
| --- | --- | --- |
| 1 | `.vscode/launch.json` | Defines the compound profile and every .NET debugger target. |
| 2 | `.vscode/tasks.json` | Defines the pre-launch task and starts `powershell.exe`. |
| 3 | `scripts/prepare-all-vscode-debug.ps1` | Stops stale GRD processes owned by this repository and protects unrelated processes. |
| 4 | `scripts/local-debug-processes.ps1` | Performs repository ownership checks, deduplication, stopping, and port-release verification. |
| 5 | `scripts/start-local-services.ps1` | Loads local settings, starts infrastructure, and builds enabled projects. |
| 6 | `deploy/docker/compose.infrastructure.yml` | Defines MySQL, RabbitMQ, their ports, health checks, networks, and volumes. |
| 7 | Each API/worker `Program.cs` | Starts that process and registers its application and infrastructure dependencies. |

These files cooperate, but they do not all launch processes. The PowerShell
preparation builds the targets; VS Code itself launches the targets under the
debugger after preparation succeeds.

## 3. Detailed F5 sequence

### Step 1 - VS Code selects the compound profile

The compound profile is in `.vscode/launch.json`:

```json
{
  "name": "GRD: Start ALL backend services with debugger",
  "configurations": [
    "GRD internal: ApiGateway",
    "GRD internal: Identity",
    "GRD internal: Notifications",
    "...",
    "GRD internal: OutboxPublisher",
    "GRD internal: EventProcessor",
    "GRD internal: ProjectionBuilder"
  ],
  "preLaunchTask": "GRD: Debug prepare ALL backend services",
  "stopAll": true
}
```

The compound does not start those targets immediately. VS Code first waits for
`GRD: Debug prepare ALL backend services` to finish.

### Step 2 - The task starts Windows PowerShell

`.vscode/tasks.json` runs:

```text
powershell.exe -NoProfile -File scripts\prepare-all-vscode-debug.ps1
```

`.ps1` means a **PowerShell script**. PowerShell is the orchestration language in
this repository. It calls programs such as `docker`, `dotnet`, and `npm`; it does
not contain the ERP business rules.

The VS Code PowerShell extension is optional for this startup. Windows
`powershell.exe` can execute the scripts without the extension.

### Step 3 - Existing GRD processes are safely restarted

`scripts/prepare-all-vscode-debug.ps1` checks:

- every configured API port;
- the OutboxPublisher process name;
- the EventProcessor process name;
- the ProjectionBuilder process name.

`scripts/local-debug-processes.ps1` resolves every listener to a process and verifies
its executable path or `dotnet` command line. When it belongs to this repository,
the stale process is stopped once (IPv4 and IPv6 listeners are deduplicated) and the
script waits for its port to be released. This prevents two different problems:

1. two processes cannot listen on the same HTTP port;
2. Windows may lock a running project's generated `.exe` and DLL files, preventing
   the build from replacing them.

If the owner belongs to another repository or is an unrelated application, the
script does not kill it. Preparation stops and reports its port and PID instead.

### Step 4 - The central runner is called in BuildOnly mode

The preparation script calls:

```powershell
scripts\start-local-services.ps1 -BuildOnly
```

`-BuildOnly` means:

- start/reuse Docker infrastructure;
- build all enabled projects sequentially;
- do not launch the application processes from PowerShell.

VS Code will launch the backend processes itself after the build completes.

### Step 5 - Local connection settings are prepared

`scripts/start-local-services.ps1` reads `deploy/docker/.env`. The current local
MySQL configuration is:

```text
Host machine: 127.0.0.1 or localhost
Host port:    3308
Container:    mysql
Container port: 3306
Database:     grd_local
User:         grd
```

The mapping is:

```text
.NET service -> localhost:3308 -> Docker port mapping -> MySQL container:3306
```

RabbitMQ uses:

```text
AMQP:          localhost:5672
Management UI: http://localhost:15672
```

The internal debugger configurations in `.vscode/launch.json` also provide these
connection strings directly to each launched process.

### Step 6 - Docker Compose starts or reuses infrastructure

The current script executes the equivalent of:

```powershell
docker compose `
  --env-file deploy\docker\.env `
  -f deploy\docker\compose.infrastructure.yml `
  up -d
```

Docker Compose manages only two containers:

| Container | What it does |
| --- | --- |
| MySQL | Stores application rows, users, Outbox rows, and Inbox rows. |
| RabbitMQ | Transfers integration events between services. |

On the first run, Docker may pull images and create containers, a network, and
persistent volumes. On later runs, `up -d` normally reuses or starts the existing
containers. It does not create a new database on every F5.

The current scripts use `up -d` without Compose `--wait`. Therefore, after Docker
Desktop or the containers have just started, verify that both services are healthy
before testing a database workflow:

```powershell
docker compose `
  --env-file .\deploy\docker\.env `
  -f .\deploy\docker\compose.infrastructure.yml `
  ps
```

### Step 7 - MySQL initialization behavior

The directory `deploy/docker/mysql/init` is mounted at
`/docker-entrypoint-initdb.d` inside MySQL. The official MySQL container executes
those `.sql` files only while creating a new, empty MySQL data volume.

Consequences:

- F5 does not reapply all SQL files every time.
- Stopping a container does not delete the database.
- Recreating only the container normally keeps data because the named volume
  remains.
- Removing the MySQL data volume deletes the local database and should be done only
  when that data is intentionally disposable.

To reapply Identity development data to an existing database, run manually:

```powershell
.\scripts\apply-local-identity-seed.ps1
```

That script is not part of the F5 path.

### Step 8 - Enabled projects are built sequentially

The central runner executes `dotnet build` for enabled .NET projects one at a time.
Sequential building avoids several services concurrently rebuilding shared
BuildingBlocks projects and trying to copy the same outputs.

Because `Web` is enabled in the central service registry, BuildOnly currently also
runs the React production build. The compound debugger still does not launch Vite
or attach a JavaScript debugger.

### Step 9 - VS Code launches backend processes

When preparation succeeds, VS Code starts every DLL listed in the compound:

- 14 API processes, including the API Gateway;
- 3 worker processes;
- 17 independent .NET processes in total.

Every process has its own entry point (`Program.cs`), dependency-injection
container, configuration, logs, lifetime, and debugger session. Shared class
libraries are not separate processes.

The worker processes have no HTTP port:

| Worker | Purpose |
| --- | --- |
| OutboxPublisher | Polls service-owned Outbox tables and publishes pending events to RabbitMQ. |
| EventProcessor | Runs background event-processing work implemented by that worker. |
| ProjectionBuilder | Runs background read-model/projection work implemented by that worker. |

## 4. Outbox is not a Docker container

The word **Outbox** refers to a reliability pattern, not to an infrastructure
container.

```text
Application command
  -> service changes its business data
  -> service writes an Outbox row in the same MySQL transaction
  -> transaction commits
  -> OutboxPublisher reads the pending row
  -> OutboxPublisher publishes the event to RabbitMQ
  -> another service consumes the event
  -> consumer records an Inbox row to prevent duplicate processing
```

Examples of service-owned tables include:

- `order_management_outbox`;
- `inventory_outbox`;
- `procurement_outbox`;
- `warehouse_outbox`.

MySQL stores the Outbox rows, RabbitMQ transports the events, and the
OutboxPublisher .NET worker connects the two.

## 5. What happens after all processes start

For a typical authenticated frontend request:

```text
React UI
  -> HTTP request to API Gateway :7000
  -> YARP forwards to the owning API
  -> Controller creates a command/query
  -> MediatR invokes an Application handler
  -> handler uses a repository interface
  -> Infrastructure repository opens MySQL through MySqlConnector/Dapper
  -> response returns through Gateway to React
```

For an asynchronous cross-service change:

```text
Service writes business data + Outbox row
  -> OutboxPublisher
  -> RabbitMQ
  -> consumer hosted inside the destination service
  -> Inbox duplicate check
  -> destination service local transaction
```

## 6. What every PowerShell script does

| Script | Automatically used by compound F5? | Purpose |
| --- | --- | --- |
| `prepare-all-vscode-debug.ps1` | Yes | Checks all API ports and worker process names, then calls the central runner in BuildOnly mode. |
| `start-local-services.ps1` | Yes, indirectly | Owns the service registry, local environment defaults, Docker startup, builds, and optional normal process launching. |
| `prepare-vscode-debug.ps1` | Only for a single-service F5 profile | Checks one service, starts infrastructure, and builds that service. |
| `apply-local-identity-seed.ps1` | No | Manually sends Identity SQL scripts to the existing MySQL container. |
| `smoke-order-inventory-workflow.ps1` | No | Runs a real end-to-end Order/Inventory workflow through the Gateway. |
| `smoke-procure-to-receive.ps1` | No | Runs a real authenticated Material Request to Goods Receipt workflow. |

The two smoke scripts are not mock tests. They use real local APIs, MySQL,
RabbitMQ, Outbox/Inbox processing, and consumers. They create real development rows
in `grd_local` and do not automatically clean those rows afterward.

## 7. F5 compared with other ways to run

| Action | Who launches processes? | Debugger attached? | React included? |
| --- | --- | ---: | ---: |
| Compound F5 | VS Code `launch.json` | Yes, all backends | No |
| Single-service F5 | VS Code `launch.json` | Yes, one backend | No |
| `GRD: Start enabled services` task | VS Code tasks | No | Yes when enabled |
| `start-local-services.ps1` without arguments | PowerShell separate consoles | No | Yes when enabled |
| Attach profile | Existing process remains owner | Yes, selected process | Separate browser profile for React |

When compound F5 is used after the normal runner, the pre-launch task stops the
copies owned by this repository before rebuilding them under the debugger. This is
intentional; use the attach profile instead when those existing processes must remain
running.

## 8. Stopping and restarting

With the compound profile:

- **F5** starts the selected debug profile or continues from a breakpoint.
- **Shift+F5** stops all processes launched by the compound because `stopAll` is
  enabled.
- stopping the debugger does not stop MySQL or RabbitMQ containers;
- an assembly change is not loaded into an already running process unless supported
  by Hot Reload; after a normal rebuild, stop and restart the affected process;
- when an exception pauses the debugger, HTTP requests to that paused process may
  appear to hang until execution is continued or the process is restarted.

## 9. Understanding MySQL connection timeouts

This exception:

```text
MySqlConnector.MySqlException: Connect Timeout expired
```

occurs before the repository can execute SQL. It means
`MySqlConnection.OpenAsync` could not complete the MySQL connection handshake.

Check in this order:

1. Docker Desktop is running.
2. `docker compose ... ps` shows MySQL as `healthy`.
3. host port `3308` is published to container port `3306`.
4. the service connection string uses port `3308`.
5. no debugger is paused inside the service.
6. stop the affected debug process and restart it to clear stale connection-pool
   state.
7. if `localhost` is unreliable through WSL/IPv6 on the machine, use
   `Server=127.0.0.1` consistently for local development.

Do not delete the MySQL container or volume merely because a service process timed
out. First verify container health and restart only the affected .NET process.

## 10. Shared error log

Development errors from all backend processes are written to:

```text
logs/grd-errors-YYYYMMDD.log
```

The service name, trace ID, span ID, message, and exception stack identify which
process failed. This is especially useful for Gateway calls because one trace can
show both the Gateway error and the downstream service error.

## 11. Common questions answered

### Does F5 create MySQL every time?

No. Compose normally reuses the existing container and persistent volume.

### Does F5 create an Outbox container?

No. Outbox rows are in MySQL; OutboxPublisher is a .NET worker.

### Does F5 start React?

The all-backend compound does not launch React. Start Vite separately.

### Are the smoke scripts mock tests?

No. They are real end-to-end tests against the local running system.

### Why can one project lock another build?

Usually it is the same project already running, or multiple projects concurrently
copying shared output dependencies. Stop the owning process before rebuilding and
build enabled projects sequentially.

### Why does a request hang while debugging?

A process paused at a breakpoint or exception cannot finish its HTTP request until
execution continues.

## 12. Source-of-truth files

Use these files when behavior changes:

- `.vscode/launch.json` - debugger profiles and compound membership;
- `.vscode/tasks.json` - VS Code task commands;
- `scripts/start-local-services.ps1` - enabled-service registry and normal runner;
- `scripts/prepare-all-vscode-debug.ps1` - compound preflight logic;
- `scripts/prepare-vscode-debug.ps1` - single-service preflight logic;
- `deploy/docker/.env` - local infrastructure ports and credentials;
- `deploy/docker/compose.infrastructure.yml` - container definitions and health checks;
- `docs/local-service-runner.md` - command-oriented local runner reference.
