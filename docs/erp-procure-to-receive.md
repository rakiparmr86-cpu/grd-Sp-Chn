# ERP foundation: Store request to purchase and material receipt

## Scope

This is the first ERP-oriented vertical slice for a multi-company, multi-region,
multi-branch manufacturing organization. It proves this business path:

```text
Production Store Supervisor logs in
  -> creates Material Request for the supervisor's organization unit
Purchase Manager logs in
  -> approves Material Request
  -> issues Purchase Order to a supplier
Procurement transaction
  -> PO + Procurement Outbox row
Outbox Publisher -> RabbitMQ procurement.events
Warehouse consumer at the production location
  -> creates Expected Purchase Order through Inbox deduplication
Production Store Supervisor
  -> verifies delivered items against the expected PO
  -> posts Goods Receipt / GRN
Warehouse transaction
  -> Expected PO Received + GRN + Warehouse Outbox row
Outbox Publisher -> RabbitMQ warehouse.events
  ├── Inventory consumer increases location stock through Inbox deduplication
  └── Procurement consumer closes PO and Material Request through Inbox deduplication
```

Version 1 deliberately supports one complete receipt per PO. Partial receipt,
over/under-delivery tolerance, rejection, quality inspection, tax, payment,
supplier validation and production consumption are future slices.

## Bounded-context ownership

| Service | Owns in this slice | Does not own |
| --- | --- | --- |
| Identity | Login, PBKDF2 password verification, JWT, role/permission/org claims | Organization hierarchy and business approvals |
| Organization | Enterprise-to-facility hierarchy | Users, stock and procurement transactions |
| Procurement | Material Request, approval, Purchase Order and procurement status | Physical receipt and stock balance |
| Warehouse | Expected PO, physical verification and GRN | Supplier negotiation and stock valuation |
| Inventory | On-hand quantity per organization location and product | PO/GRN documents |
| Outbox Publisher | Reliable publishing from service-owned Outbox tables | Business decisions |
| API Gateway | Public HTTP routing and header forwarding | Authentication decisions and business rules |

Services never read or write another service's tables. RabbitMQ contracts contain
only the identifiers and line details needed by their consumers.

## Organization hierarchy

The seed data in
[`002_erp_procure_to_receive.sql`](../deploy/docker/mysql/init/002_erp_procure_to_receive.sql)
creates:

```text
GRD Enterprise
└── Head Office
    └── Regional Office North
        └── Delhi Head Branch
            └── Delhi Branch
                ├── Delhi Manufacturing Plant
                ├── Delhi Warehouse
                ├── Delhi Boiler Consumption Unit
                └── Delhi Sales Branch
```

`OrganizationUnit` is a management/facility hierarchy. Procurement, Warehouse and
Inventory store its identifier; they do not copy the hierarchy. In the current
slice a user has one assigned organization unit, and Warehouse requires the token's
organization unit to exactly equal the PO destination. Descendant/ancestor access
grants for regional and head-office users must be implemented before multi-region
production rollout.

## Roles and permissions

A role is the person's grade; permissions control actions. Do not infer every
permission only from seniority.

| Local account | Role | Organization | Main actions |
| --- | --- | --- | --- |
| `director@grd.local` | Director | Head Office | All first-slice actions and hierarchy management |
| `gm.north@grd.local` | General Manager | North Region | Approve/read requests and issue/read POs |
| `manager.purchase@grd.local` | Manager | Delhi Head Branch | Approve/read requests and issue/read POs |
| `supervisor.plant@grd.local` | Supervisor | Delhi Manufacturing Plant | Create/read requests, receive goods, read location stock |
| `executive.boiler@grd.local` | Executive | Delhi Boiler Unit | Create/read requests |

The local password for every seeded account is `1223456`. These users and the
development JWT key are demonstration values only. Production must use unique user
passwords, a secret manager, key rotation, refresh/revocation and administrative
user/permission APIs.

## HTTP APIs through the Gateway

| User action | Gateway request | Required permission |
| --- | --- | --- |
| Login | `POST /api/identity/auth/login` | Anonymous |
| Read hierarchy | `GET /api/organization/units` | `organization.read` |
| Create hierarchy node | `POST /api/organization/units` | `organization.manage` |
| Create request | `POST /api/procurement/material-requests` | `procurement.material-request.create` |
| Read request | `GET /api/procurement/material-requests/{id}` | `procurement.material-request.read` |
| Approve request | `POST /api/procurement/material-requests/{id}/approve` | `procurement.material-request.approve` |
| Issue PO | `POST /api/procurement/material-requests/{id}/purchase-orders` | `procurement.purchase-order.create` |
| Read PO | `GET /api/procurement/purchase-orders/{id}` | `procurement.purchase-order.read` |
| View expected PO | `GET /api/warehouses/purchase-orders/{id}` | `warehouse.goods-receipt.read` |
| Post GRN | `POST /api/warehouses/purchase-orders/{id}/goods-receipts` | `warehouse.goods-receipt.post` |
| Read location stock | `GET /api/inventory/stock/locations/{organizationUnitId}/{productId}` | `inventory.stock.read` |

The API derives the current `userId` and `organizationUnitId` from the validated JWT.
The browser is never allowed to submit those security-sensitive identities.

## Local setup

From `D:\newdata\grd-Sp-Chn`, start MySQL and RabbitMQ as described in the root
README. A fresh MySQL volume applies both initialization scripts automatically.
For an existing volume, apply the new schema without deleting data:

```powershell
Get-Content -Raw deploy\docker\mysql\init\002_erp_procure_to_receive.sql |
  docker compose -f deploy\docker\compose.infrastructure.yml exec -T mysql `
    mysql -ugrd -pgrd-local grd_local
```

Start these processes:

```text
API Gateway       7000
Identity          7001
Inventory         5018
Organization      5218
Procurement       5112
Warehouse         5276
Outbox Publisher  background worker
```

The VS Code task can start all enabled services. To run only this slice, open one
terminal per process and use, for example:

```powershell
.\scripts\start-local-services.ps1 -Service Identity -SkipInfrastructure
```

Replace `Identity` with each process name above. The first terminal may start the
containers; use `-SkipInfrastructure` in the remaining terminals.

After all processes are running, execute:

```powershell
.\scripts\smoke-procure-to-receive.ps1
```

The script calls only Gateway port `7000` and verifies the final Procurement status
and production-location stock.

## Reliability and scaling behavior

- Procurement writes PO and Outbox together.
- Warehouse uses Inbox before creating its expected PO.
- Warehouse writes GRN, PO status and Outbox together.
- Inventory and Procurement use Inbox before applying the GRN event.
- Every API is stateless and can be replicated behind the Gateway/load balancer.
- Service-owned indexes include organization/status or location/product keys.
- Reporting should consume events into read models instead of joining service tables.

Before thousands-user production usage, add hierarchical access-scope grants,
approval delegation, optimistic version columns, audit history, distributed tracing,
rate limiting, production secret/key management, database-per-service deployment,
RabbitMQ high availability and realistic concurrency/load tests.
