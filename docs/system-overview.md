# GRD Supply Chain system overview

## Purpose of this document

Use this document to answer these questions before changing or running the solution:

- How many services and executable processes exist?
- Which projects contain implemented business behavior and which are scaffolds?
- What does each API, worker, container, and shared building block do?
- How does an HTTP request or RabbitMQ event move through the system?
- Which technologies implement each architectural concern?
- What must be updated when a new service or process is introduced?

For commands that start and stop the processes, see
[`local-service-runner.md`](local-service-runner.md). For detailed event-driven
design rules, see [`event-driven-development.md`](event-driven-development.md).

## Current repository inventory

| Category | Count | Explanation |
| --- | ---: | --- |
| Business service boundaries | 13 | Delivery, Identity, Inventory, Notifications, Order Management, Organization, Procurement, Product Catalog, Reporting, Shipment, Supplier, Transportation, and Warehouse. |
| Projects per service | 4 | Each service has `.Api`, `.Application`, `.Domain`, and `.Infrastructure`. |
| Service projects | 52 | 13 services multiplied by four layers. |
| API Gateway projects/processes | 1 | YARP public entry point. |
| Frontend projects/processes | 1 | React/Vite login and permission-aware ERP dashboard. |
| Worker projects/processes | 3 | Outbox Publisher, Event Processor, and Projection Builder. |
| Shared building-block projects | 7 | Contracts, EventBus abstractions, RabbitMQ adapter, MySQL persistence, observability, JWT security, and Shared Kernel. |
| Test projects | 4 | Unit, architecture, contract, and integration tests. |
| Total `.csproj` files | 67 | Includes services, Gateway, workers, building blocks, and tests. |

When every executable is enabled locally, the runtime consists of:

```text
14 backend HTTP processes = 1 API Gateway + 13 service APIs
 1 frontend HTTP process = React/Vite web UI
 3 worker processes
 2 infrastructure containers = MySQL + RabbitMQ
---------------------------------------------------
20 runtime processes/containers in the full local landscape
```

Building blocks, Domain, Application, Infrastructure, and test projects compile to
assemblies; they are not separate running processes.

## Implementation-status legend

| Status | Meaning |
| --- | --- |
| **Implemented** | Contains working behavior used by the Order-to-Inventory vertical slice. |
| **Partial** | Starts and exposes limited health/sample behavior, but its intended business capability is not implemented. |
| **Scaffold** | Has the standard four-layer structure and template endpoint only. Do not assume the intended business behavior exists. |

## Executable process catalog

### Frontend

| Process | Port | Status | Responsibility |
| --- | ---: | --- | --- |
| `grd-spchn-web` | 5173 | **Partial** | React login, permission-aware ERP command center, HR user creation, and Director access-profile permission management. Calls backend services only through YARP. Procurement forms and operational read models remain. |

### API Gateway

| Process | Port | Status | Responsibility |
| --- | ---: | --- | --- |
| `GRD.SpChn.ApiGateway` | 7000 | **Implemented** | Public YARP entry point. Matches public routes, removes configured prefixes, and forwards requests to the owning API. It contains no business rules. |

The Gateway also exposes its own `/`, `/health/live`, and `/health/ready` endpoints.

### Service APIs

| Service process | Port | Gateway path | Status | Current responsibility |
| --- | ---: | --- | --- | --- |
| `GRD.SpChn.OrderManagement.Api` | 5255 | `/orders/{**catch-all}` | **Implemented** | Accepts orders, returns `Pending`, exposes order-status queries, consumes Inventory reservation results, and confirms/cancels Orders. |
| `GRD.SpChn.Inventory.Api` | 5018 | `/api/inventory/{**catch-all}` | **Implemented** | Handles the sales-order reservation flow and consumes Warehouse GRNs to maintain location/product stock. |
| `GRD.SpChn.Identity.Api` | 7001 | `/api/identity/{**catch-all}` | **Partial** | Authenticates PBKDF2-backed users; resolves role and permissions from database-owned access-profile tables; issues organization/role/permission JWTs; lets authorized HR Managers create operational users; and lets Directors atomically manage profile permissions from a validated catalog. Refresh, revocation, audit and production key management remain. |
| `GRD.SpChn.Notifications.Api` | 7002 | `/api/notifications/{**catch-all}` | **Partial** | Has health/sample endpoints. Email, SMS, templates, delivery status, and event consumers are not implemented. |
| `GRD.SpChn.ProductCatalog.Api` | 5006 | `/api/products/{**catch-all}` | **Scaffold** | Intended to own product definitions, attributes, and catalog queries; currently only template behavior exists. |
| `GRD.SpChn.Shipment.Api` | 5059 | `/api/shipments/{**catch-all}` | **Scaffold** | Intended to own shipment planning and shipment state; currently only template behavior exists. |
| `GRD.SpChn.Procurement.Api` | 5112 | `/api/procurement/{**catch-all}` | **Implemented** | Owns the first ERP slice: Material Request, approval, Purchase Order, Outbox publishing and GRN-driven closure. |
| `GRD.SpChn.Supplier.Api` | 5141 | `/api/suppliers/{**catch-all}` | **Scaffold** | Intended to own supplier information and supplier-facing operations; currently only template behavior exists. |
| `GRD.SpChn.Organization.Api` | 5218 | `/api/organization/{**catch-all}` | **Partial** | Owns and validates Enterprise, office, branch, plant, warehouse, sales and consumption-unit hierarchy nodes. Hierarchical access grants remain. |
| `GRD.SpChn.Transportation.Api` | 5258 | `/api/transportation/{**catch-all}` | **Scaffold** | Intended to own transportation planning/tracking; currently only template behavior exists. |
| `GRD.SpChn.Reporting.Api` | 5274 | `/api/reports/{**catch-all}` | **Scaffold** | Intended to expose reporting/read-model queries; currently only template behavior exists. |
| `GRD.SpChn.Warehouse.Api` | 5276 | `/api/warehouses/{**catch-all}` | **Implemented** | Consumes issued POs, creates expected receipts through Inbox, posts complete GRNs and publishes them through Outbox. |
| `GRD.SpChn.Delivery.Api` | 5294 | `/api/delivery/{**catch-all}` | **Scaffold** | Intended to own last-mile delivery execution/status; currently only template behavior exists. |

Every service API also maps common `/health/live` and `/health/ready` endpoints through
`GRD.SpChn.Observability`.

### Background workers

| Worker process | HTTP port | Status | Current responsibility |
| --- | ---: | --- | --- |
| `GRD.SpChn.OutboxPublisher` | None | **Implemented** | Polls Order, Inventory, Procurement and Warehouse Outboxes, publishes due messages to RabbitMQ, marks confirmed messages processed, and records retry state after failure. |
| `GRD.SpChn.EventProcessor` | None | **Scaffold** | Registers MySQL/RabbitMQ building blocks but currently only writes a timed heartbeat log. It does not process business events yet. |
| `GRD.SpChn.ProjectionBuilder` | None | **Scaffold** | Registers MySQL/RabbitMQ building blocks but currently only writes a timed heartbeat log. It does not build a read projection yet. |

RabbitMQ consumers for the implemented vertical slice do not run in these scaffold
workers. They are hosted inside the Inventory API and Order Management API processes.

### Infrastructure containers

| Container | Default host port | Responsibility |
| --- | ---: | --- |
| MySQL 8.4 | 3306 | Stores Orders, Stock, Inbox, and Outbox rows for the current local workflow. The demo uses one container/schema with service-prefixed tables; ownership rules still prohibit one service from writing another service's tables. |
| RabbitMQ 4 Management | 5672 | Carries Integration events through durable exchanges/queues with manual acknowledgement and retry/dead-letter handling. |
| RabbitMQ management UI | 15672 | Browser interface for exchanges, queues, connections, and dead-letter inspection; it is part of the RabbitMQ container, not another application process. |

## Minimum processes for the implemented order workflow

You do not need all 20 runtime components to test the current vertical slice. Enable:

```text
MySQL container
RabbitMQ container
API Gateway
Order Management API
Inventory API
Outbox Publisher worker
```

That is four .NET processes plus two containers. Identity, Notifications, the nine
scaffold APIs, Event Processor, and Projection Builder are not required for
`POST /orders -> reservation -> final order status`.

## Implemented business flow

### User adds stock

```text
Warehouse/operator client
  -> PUT http://localhost:7000/api/inventory/stock/{productId}
  -> YARP removes /api/inventory
  -> Inventory StockController
  -> SetStockCommand
  -> validation + transaction behaviors
  -> StockItem Domain model
  -> Inventory repository
  -> MySQL stock row
```

### User places and checks an order

```text
Client
  -> POST http://localhost:7000/orders
  -> YARP -> Order Management API
  -> CreateOrderCommand
  -> Order aggregate creates Pending order
  -> one MySQL transaction writes Order + Order Outbox
  <- 202 Accepted, Pending, order id and Location

Outbox Publisher
  -> reads Order Outbox
  -> publishes OrderPlaced to RabbitMQ/order.events

Inventory API consumer
  -> Inbox duplicate check
  -> ReserveStockCommand
  -> locks/checks all stock items
  -> writes stock changes + StockReserved Outbox
     or no stock changes + StockReservationFailed Outbox

Outbox Publisher
  -> publishes result to RabbitMQ/inventory.events

Order Management API consumer
  -> Inbox duplicate check
  -> confirms or cancels Order

Client
  -> GET http://localhost:7000/orders/{orderId}
  <- Pending | Confirmed | Cancelled
```

No distributed transaction spans the services. Each business database change and its
Outbox intent commit in one local transaction. Consumers use Inbox records so an
at-least-once RabbitMQ delivery does not apply the same side effect twice.

## Project architecture inside one service

Every service boundary follows this dependency direction:

```text
                 +------------------+
HTTP/RabbitMQ -->|       .Api       |  composition root and transport mapping
                 +--------+---------+
                          |
                          v
                 +------------------+
                 |   .Application   |  commands, queries, handlers, ports
                 +--------+---------+
                          |
                          v
                 +------------------+
                 |      .Domain     |  aggregates, entities, invariants
                 +------------------+

                 +------------------+
                 | .Infrastructure  |  repositories, MySQL, Inbox/Outbox,
                 +--------+---------+  RabbitMQ consumer registration
                          |
                          +---- implements Application-owned interfaces
```

Rules:

- `.Domain` has no dependency on outer layers.
- `.Application` references Domain and shared abstractions/contracts, never concrete
  Infrastructure.
- `.Infrastructure` implements Application ports.
- `.Api` is the composition root and contains transport mapping, not business rules.
- A service must not reference another service's Domain/Application/Infrastructure.
- Cross-service facts use versioned contracts from `GRD.SpChn.Contracts`.

Architecture tests enforce the important project-reference rules.

## Shared building blocks

| Project | Purpose | Must not contain |
| --- | --- | --- |
| `GRD.SpChn.Contracts` | Immutable/versioned Integration-event contracts and topology names shared between producers and consumers. | Domain aggregates, database models, service handlers. |
| `GRD.SpChn.EventBus.Abstractions` | Transport-neutral `IEventBus` and `IIntegrationEventHandler<T>` ports. | RabbitMQ-specific connection/channel code. |
| `GRD.SpChn.EventBus.RabbitMQ` | RabbitMQ publishing, consumers, acknowledgement, retry, topology, and dead-letter behavior. | Service-specific business decisions. |
| `GRD.SpChn.Persistence.MySql` | MySQL connection factory and common persistence registration. | Service-owned SQL/repository behavior. |
| `GRD.SpChn.Observability` | Serilog console/request logging, Problem Details, and common liveness/readiness endpoints. | Service-specific business logging decisions. |
| `GRD.SpChn.Security` | JWT issuance/validation, standard claims, permissions and policies shared by APIs. | User persistence or service-specific authorization decisions. |
| `GRD.SpChn.SharedKernel` | Small cross-cutting primitives such as `Result<T>` and `Error`. | Service-specific entities or workflows. |

Shared building blocks reduce technical duplication. They must not become a shared
business-logic monolith.

## Technology map

| Concern | Current technology | Where it is used |
| --- | --- | --- |
| Runtime/language | .NET 10 / C# | All application, worker, building-block, and test projects. |
| HTTP APIs | ASP.NET Core controllers/minimal endpoints | Service `.Api` projects and Gateway health/root endpoints. |
| API Gateway | YARP 2.3 | `GRD.SpChn.ApiGateway`, routes configured in `appsettings.Development.json`. |
| Commands/queries | MediatR 14.2 | Service `.Application` projects. |
| Input validation | FluentValidation 12.1 | Application pipeline before handlers/transactions. |
| Relational database | MySQL 8.4 | Docker local infrastructure. |
| Database driver | MySqlConnector 2.6 | Shared MySQL persistence and Outbox Publisher. |
| SQL mapping | Dapper 2.1 | Infrastructure repositories and workers. |
| Message broker | RabbitMQ 4 | Integration-event transport. |
| RabbitMQ client | RabbitMQ.Client 7.2 | Shared RabbitMQ adapter. |
| Reliability | Outbox, Inbox, Unit of Work, manual acknowledgements, bounded retry, dead-letter queues | Implemented Order/Inventory and Procure-to-Receive slices. |
| Security | JWT bearer authentication + permission policies | Identity issues organization-scoped development tokens; protected ERP endpoints validate them. |
| Logging | Serilog.AspNetCore 10 | Shared observability defaults and console/request logs. |
| Health | ASP.NET Core Health Checks | `/health/live` and `/health/ready`. |
| API discovery and testing | ASP.NET Core OpenAPI + Swashbuckle Swagger UI | Development-only `/swagger` on every API, with base-address redirects. Gateway `/swagger` provides a dark, same-origin selector for all 13 service documents. |
| Local infrastructure | Docker Compose | MySQL and RabbitMQ. |
| Tests | xUnit | Unit, contract, architecture, and integration test projects. |
| Local orchestration | PowerShell + VS Code tasks | `scripts/start-local-services.ps1` and `.vscode/tasks.json`. |

Not currently implemented as production capabilities: OpenTelemetry distributed
tracing, production identity lifecycle/key management, persisted Saga storage, Polly
HTTP circuit breakers, Kubernetes manifests, or business logic for the remaining
scaffold services.

## Configuration ownership

| Configuration | Source |
| --- | --- |
| API ports | Each executable's `Properties/launchSettings.json`. |
| Public Gateway paths and destinations | `ApiGateway/.../appsettings.Development.json`. |
| Enabled local processes | `scripts/start-local-services.ps1`. |
| VS Code terminal orchestration | `.vscode/tasks.json`. |
| MySQL/RabbitMQ containers and ports | `deploy/docker/compose.infrastructure.yml` and optional `.env`. |
| Order/Inventory schema | `deploy/docker/mysql/init/001_order_inventory_workflow.sql`. |
| ERP hierarchy/procurement/receipt schema | `deploy/docker/mysql/init/002_erp_procure_to_receive.sql`. |
| Outbox sources/polling | `Workers/GRD.SpChn.OutboxPublisher/appsettings.json`. |
| RabbitMQ retry settings | Implemented API `appsettings*.json` plus environment variables. |

Environment variables override local defaults. Never commit production passwords or
connection strings to these files.

## Known gaps that future developers must not miss

1. Six service APIs are scaffolds. A running process and a `200` WeatherForecast
   response do not mean its business capability exists.
2. Event Processor and Projection Builder are heartbeat templates only.
3. Identity supports the first-slice login/JWT flow, but production user lifecycle,
   password reset/MFA, refresh/revocation, key rotation and hierarchical scope grants
   remain to be implemented.
4. Notifications does not send messages yet.
5. ERP endpoints use real permission policies. Existing Order endpoints are still
   anonymous and require an explicit migration plan before production exposure.
6. Readiness currently includes only registered checks; add MySQL/RabbitMQ checks
   deliberately when deployment requirements are defined.
7. The current local workflow uses one MySQL container/schema. Preserve service table
   ownership and move toward separately deployable service databases as modules are
   implemented.
8. Do not activate scaffolded Integration-event contracts until a real producer,
   consumer, business reaction, version strategy, and operational owner exist.

## Checklist for adding a new service or process

- [ ] Define the bounded context, owner, aggregate, commands/queries, and database
      ownership before scaffolding.
- [ ] Add `.Api`, `.Application`, `.Domain`, and `.Infrastructure` only when the
      process needs those layers.
- [ ] Assign a unique local port in `launchSettings.json`.
- [ ] Add a YARP route only for a client-facing API.
- [ ] Add the process to `scripts/start-local-services.ps1` and `.vscode/tasks.json`.
- [ ] Update the process counts and status in this document.
- [ ] Add liveness/readiness behavior and structured logging.
- [ ] Define synchronous HTTP versus Integration-event communication explicitly.
- [ ] Add contracts only for identified cross-service consumers.
- [ ] Add Domain, Application, contract, architecture, integration, and end-to-end
      tests appropriate to the new behavior.
- [ ] Document required environment variables, database migration, queues, retry,
      dead-letter handling, and operations runbook.

## Documentation map

| Document | Use it for |
| --- | --- |
| [`README.md`](../README.md) | Quick start, prerequisites, current Order/Inventory smoke flow. |
| [`erp-procure-to-receive.md`](erp-procure-to-receive.md) | First ERP hierarchy, login, role, Material Request, PO, GRN and location-stock flow. |
| [`local-service-runner.md`](local-service-runner.md) | Starting/stopping processes and disabling services. |
| [`event-driven-development.md`](event-driven-development.md) | Architecture rules, patterns, transaction boundaries, and delivery guarantees. |
| [`development-phases/phase-4-commands-queries.md`](development-phases/phase-4-commands-queries.md) | Application commands, queries, handler tests, and user scenarios. |
| [`development-phases/phase-6-integration-event-contracts.md`](development-phases/phase-6-integration-event-contracts.md) | Contract payloads, package/wire versioning, and compatibility tests. |
| [`adr/README.md`](adr/README.md) | Architecture Decision Record index. |
| [`infrastructure.md`](infrastructure.md) | Schema, environment, broker, health, and deployment configuration details. |
