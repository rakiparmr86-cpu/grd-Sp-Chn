# ADR 0005: Co-host Inventory and Warehouse modules

- **Status:** Proposed
- **Date:** 2026-09-09

## Context

Goods receipt, quality inspection, quarantine release and location stock are one
tightly connected operational capability. Running Warehouse and Inventory as two
services added a RabbitMQ hop between a passed quality inspection and the stock
balance update. That created avoidable operational failure modes and required two
processes even though both modules currently use the same local MySQL database.

Procurement, Supplier and Accounting remain different business boundaries: they
have independent workflows, security rules and scaling/change characteristics.

## Decision

Warehouse is an internal module of the Inventory deployable. The single runtime is
`GRD.SpChn.Inventory.Api` on port `5018` and it contains four explicit modules:

1. Receiving / GRN;
2. Quality and quarantine;
3. Warehouse operations;
4. Inventory stock and stock ledger.

The existing `GRD.SpChn.Warehouse.Domain`, `.Application` and `.Infrastructure`
assemblies remain as module boundaries during the migration. The old
`GRD.SpChn.Warehouse.Api` project is a legacy host and is not started by F5 or the
local service runner.

For compatibility, YARP routes both `/api/inventory/*` and `/api/warehouses/*` to
the Inventory process. Clients do not need an immediate URL migration.

Passing quality writes the quality decision, immutable stock movement, location
stock balance and `QualityInspectionApprovedIntegrationEvent` Outbox row in one
local database transaction. The merged process does not consume its own quality
event. The event is still published after commit for Procurement, Notifications,
Reporting and the future Accounting service.

The existing `warehouse_*` table names and `warehouse_outbox` are retained to avoid
a risky data migration. They are now owned by the Inventory Management deployable,
not by a separate Warehouse service.

## Consequences

- One fewer API process and no Warehouse port `5276` during normal development.
- Quality approval cannot commit without its corresponding stock update.
- Receiving/Quality and Stock remain separate code modules and can be extracted
  later if their scale or release cadence genuinely diverges.
- Procurement must continue to react through the published integration event; it
  must not read Inventory Management tables.
- The quality-approved event is an external notification, not an internal command.

## Rejected alternatives

- **Keep two services and use synchronous HTTP:** still introduces a distributed
  transaction failure between quality and stock.
- **Keep the self-published RabbitMQ event:** eventual consistency is unnecessary
  inside one deployable and makes the user wait for usable stock.
- **Merge Procurement as well:** its PO/approval lifecycle is an independent
  boundary and should remain asynchronously coupled.
