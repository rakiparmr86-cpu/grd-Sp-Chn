# ADR 0004: Integration-event contract conventions

- **Status:** Proposed
- **Date:** 2026-08-21
- **Decision owners:** Architecture owner and producer/consumer representatives
- **Scope:** Events published outside their owning service

## Context

Integration events are public contracts between independently deployed services.
Inconsistent names, mutable semantics, missing identity, or in-place breaking changes
make consumers fragile and make safe deployment ordering impossible.

Domain events are internal implementation details and must not be confused with
integration contracts.

## Decision

### Naming

1. Events describe facts in past tense: `OrderPlaced`, `StockReserved`,
   `StockReservationFailed`.
2. .NET contract types end in `IntegrationEvent`, for example
   `OrderPlacedIntegrationEvent`.
3. Exchanges identify the publishing business capability, for example
   `order.events` and `inventory.events`.
4. Routing keys use lowercase dot-separated facts, for example `order.placed` and
   `inventory.stock-reserved`.
5. Queues are owned and named by the consumer, for example
   `inventory.order-placed`. A producer never publishes directly to a consumer queue.
6. Commands or requests to make another service do work are not named as events.

### Required contract data

Every integration event contains:

- globally unique `EventId`;
- `OccurredOnUtc` stored and serialized as UTC;
- stable business identifiers needed by consumers;
- only the minimum business data needed to process the fact.

Correlation id, causation id, producer name, schema version, and trace context should
be added to a shared message envelope before additional production workflows are
introduced. Sensitive data and credentials must not be included unless explicitly
approved and protected.

### Domain-to-integration mapping

The Application layer maps an internal domain event to an integration event. Domain
aggregates, persistence rows, and domain events are never serialized directly onto
RabbitMQ.

### Versioning

1. The existing unversioned event and routing key are treated as version 1.
2. Backward-compatible additive changes may keep the same version when new fields are
   optional or have safe defaults for older consumers.
3. Removing/renaming a field, changing its type, changing meaning, or changing a
   required invariant is a breaking change.
4. A breaking change creates a new type and routing key, for example
   `OrderPlacedIntegrationEventV2` and `order.placed.v2`.
5. Producer and consumers support old and new versions in parallel during migration.
6. The old version is removed only after consumer ownership is known, usage is zero,
   retention/replay requirements are satisfied, and deprecation is documented.
7. Reusing an old event name with new semantics is prohibited.

### Compatibility ownership

- The producer owns the contract definition and publishes its lifecycle plan.
- Each consumer owns its queue and declares supported versions.
- Producer and consumer representatives review breaking changes together.
- Contract tests verify serialization, required fields, and supported versions.

## Alternatives considered

### Publish domain objects directly

Rejected. Internal refactoring would become a breaking external contract change and
could expose data unintentionally.

### Put a version number on every initial event name

Not adopted for existing v1 contracts to avoid unnecessary churn. New breaking
versions are explicit.

### Mutate one event schema in place

Rejected. Independently deployed consumers cannot all upgrade atomically.

## Consequences

### Positive

- Event intent, producer, routing, and ownership are understandable.
- Services can deploy contract migrations in a safe order.
- Replay and Inbox deduplication have stable message identity.

### Trade-offs

- Breaking migrations temporarily require multiple event versions and handlers.
- Contract governance and deprecation tracking are required.

## Enforcement

- Contracts live in `GRD.SpChn.Contracts/IntegrationEvents`.
- Topology names live in `MessagingTopology`.
- Contract tests protect serialization compatibility.
- Code review rejects imperative event names, missing `EventId`, local timestamps,
  domain-object payloads, and undocumented breaking changes.
