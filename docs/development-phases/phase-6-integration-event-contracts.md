# Phase 6 - Integration Event Contracts

## Phase outcome

The Order/Inventory workflow has immutable, explicitly versioned Integration-event
records in the independently buildable `GRD.SpChn.Contracts` project. Order
Management and Inventory both reference that central project; neither service owns a
private copy of the contracts.

Phase 6 defines and verifies message shape only. It does not require RabbitMQ,
Outbox polling, a database, or a running service.

## Objective -> Design -> Implement -> Test -> Deliverable -> DoD

### Objective

Define the smallest stable public contracts needed for Order Management and
Inventory to communicate without referencing each other's Domain or Application
projects.

### Design

All three contracts are `public sealed record` types. Positional record properties
and envelope properties are init-only, so a constructed message cannot be mutated by
normal application code.

| V1 contract | Producer | Consumer | Required business payload |
| --- | --- | --- | --- |
| `OrderPlacedIntegrationEvent` | Order Management | Inventory | `OrderId`, stable `OrderNumber`, `CustomerId`, and requested item `ProductId`/`Quantity`. |
| `StockReservedIntegrationEvent` | Inventory | Order Management | `ReservationId`, `OrderId`, and reserved item `ProductId`/`Quantity`. |
| `StockReservationFailedIntegrationEvent` | Inventory | Order Management | `OrderId` and business-safe failure `Reason`. |

Every contract also inherits this shared envelope:

| Field | Purpose |
| --- | --- |
| `SchemaVersion` | Identifies the JSON wire schema; defaults to `1`. |
| `EventId` | Globally unique message identity used for Inbox deduplication. |
| `OccurredOnUtc` | UTC time when the producer's fact occurred. |

The payload contains identifiers and data required to react. It does not contain an
Order aggregate, Stock aggregate, persistence entity, navigation graph, repository
model, or service-internal Domain event.

The v1 fields above remain stable because removing a published field is breaking even
if the current consumer does not read it. A future cleanup that removes fields must
be introduced as a new event version after consumer ownership is verified.

### Current v1 wire examples

The RabbitMQ adapter uses `JsonSerializerDefaults.Web`, so JSON property names are
camel case.

`OrderPlacedIntegrationEvent`:

```json
{
  "orderId": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
  "orderNumber": "ORD-20260825-001",
  "customerId": "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
  "items": [
    {
      "productId": "cccccccc-cccc-cccc-cccc-cccccccccccc",
      "quantity": 2
    }
  ],
  "schemaVersion": 1,
  "eventId": "11111111-1111-1111-1111-111111111111",
  "occurredOnUtc": "2026-08-25T10:30:00Z"
}
```

`StockReservedIntegrationEvent`:

```json
{
  "reservationId": "dddddddd-dddd-dddd-dddd-dddddddddddd",
  "orderId": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
  "items": [
    {
      "productId": "cccccccc-cccc-cccc-cccc-cccccccccccc",
      "quantity": 2
    }
  ],
  "schemaVersion": 1,
  "eventId": "22222222-2222-2222-2222-222222222222",
  "occurredOnUtc": "2026-08-25T10:30:01Z"
}
```

`StockReservationFailedIntegrationEvent`:

```json
{
  "orderId": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
  "reason": "Insufficient stock.",
  "schemaVersion": 1,
  "eventId": "33333333-3333-3333-3333-333333333333",
  "occurredOnUtc": "2026-08-25T10:30:02Z"
}
```

JSON property order is not part of the contract. Property names, types, meanings,
required identifiers, and version behavior are part of the contract.

### Versioning scheme

There are two independent versions:

1. **Package version:** `GRD.SpChn.Contracts` follows Semantic Versioning, starting
   at `1.0.0`. It controls how consuming .NET projects obtain contract code.
2. **Wire schema version:** each JSON event carries `schemaVersion`. The existing
   unversioned type and routing key are v1.

A compatible additive change may remain v1 only when the new field is optional or
has a safe default and old consumers ignore it. Examples include an optional
diagnostic code or optional correlation metadata.

A change is breaking when it removes/renames a field, changes its type or meaning,
makes an optional field required, or changes an invariant. Breaking evolution uses:

```text
OrderPlacedIntegrationEventV2
routing key: order.placed.v2
schemaVersion: 2
```

The producer publishes v1 and v2 during migration. Each consumer owns its queue and
upgrades independently. V1 is retired only after usage, replay retention, and every
consumer are verified.

For compatibility with payloads written before explicit version metadata was added,
a missing `schemaVersion` defaults to `1`.

### Implemented files

| File | Responsibility |
| --- | --- |
| `IntegrationEvents/IIntegrationEvent.cs` | Requires schema version, message id, and UTC occurrence time. |
| `IntegrationEvents/IntegrationEvent.cs` | Supplies v1 default, unique identity, and UTC timestamp. |
| `IntegrationEvents/OrderPlacedIntegrationEvent.cs` | Order-to-Inventory v1 fact and item DTO. |
| `IntegrationEvents/StockReservedIntegrationEvent.cs` | Successful Inventory result v1 fact and item DTO. |
| `IntegrationEvents/StockReservationFailedIntegrationEvent.cs` | Failed Inventory result v1 fact. |
| `GRD.SpChn.Contracts.csproj` | Defines independent build and NuGet package metadata. |
| `ContractTests/IntegrationEventContractTests.cs` | Protects naming, envelope, round-trip, required fields, and compatibility. |

The placeholder `Class1` API was removed so it is not accidentally published as part
of the contract package.

### Contract tests

The tests use the same `JsonSerializerDefaults.Web` settings as the RabbitMQ adapter
and verify:

1. Every Integration event is sealed, uses the naming convention, and inherits the
   shared envelope.
2. Envelope ids are unique, timestamps are UTC, and schema version defaults to v1.
3. All three Phase 6 contracts serialize and deserialize without losing data.
4. Required v1 JSON property names remain present; additive optional fields remain
   allowed.
5. A historical payload without `schemaVersion` still deserializes as v1.
6. A v1 reader ignores an unknown additive field from a newer compatible producer.
7. The Contracts assembly has no GRD service/layer dependency.

Run from the repository root:

```powershell
dotnet test tests\backend\GRD.SpChn.ContractTests\GRD.SpChn.ContractTests.csproj
```

### Package and consume

Build and create the versioned NuGet package:

```powershell
dotnet build `
  src\backend\BuildingBlocks\GRD.SpChn.Contracts\GRD.SpChn.Contracts.csproj

dotnet pack `
  src\backend\BuildingBlocks\GRD.SpChn.Contracts\GRD.SpChn.Contracts.csproj `
  --configuration Release `
  --output artifacts\packages
```

The result is `GRD.SpChn.Contracts.1.0.0.nupkg`. The `artifacts` directory and NuGet
packages are intentionally ignored by Git.

Inside this monorepo, both Application projects use a `ProjectReference`:

- `OrderManagement.Application/GRD.SpChn.OrderManagement.Application.csproj`;
- `Inventory.Application/GRD.SpChn.Inventory.Application.csproj`.

If a service moves to a different repository, CI publishes the immutable package to
the approved internal feed and that repository uses a centrally managed version:

```xml
<PackageReference Include="GRD.SpChn.Contracts" Version="1.0.0" />
```

Do not combine a `ProjectReference` and `PackageReference` to this contract assembly
in the same consuming project.

### Deliverable

- Independently buildable and packable `GRD.SpChn.Contracts` project.
- Explicit v1 wire envelope and documented breaking-version strategy.
- Three immutable Order/Inventory Integration-event contracts.
- Contract tests that protect serialization and backward compatibility.
- Shared reference used by both participating Application projects.

### Definition of Done

- [x] Contracts live centrally under `GRD.SpChn.Contracts/IntegrationEvents`.
- [x] Required events are sealed records with minimal stable payloads.
- [x] Every payload contains `schemaVersion`, `eventId`, and `occurredOnUtc`.
- [x] Historical payloads without explicit schema version remain readable as v1.
- [x] Round-trip and compatibility tests pass without RabbitMQ or service startup.
- [x] Contracts compile and pack independently of every service.
- [x] Package and wire-schema versioning rules are documented.
- [x] Order Management and Inventory reference the central Contracts project.

### Explicitly not part of Phase 6

- Publishing to RabbitMQ.
- Outbox persistence or Outbox polling.
- Queue/exchange declaration and consumer registration.
- Inbox deduplication and message processing.
- Database transactions or running service APIs.
- Publishing a NuGet package to an external/internal feed without configured release
  credentials and approval.

The repository contains later-phase messaging code, but ContractTests do not start or
depend on it.
