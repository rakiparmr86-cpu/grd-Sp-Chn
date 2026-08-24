# ADR 0001: Service communication boundaries

- **Status:** Proposed
- **Date:** 2026-08-21
- **Decision owners:** Architecture owner and service-team representatives
- **Scope:** All GRD Supply Chain services, workers, and API Gateway routes

## Context

GRD Supply Chain is split into independently owned services. Without a communication
rule, services can become coupled through project references, database access, or
long synchronous HTTP call chains. That coupling makes deployment risky and allows a
failure in one service to block unrelated work.

The first vertical slice needs one client command and two asynchronous service facts:

```text
Client -> Gateway -> Order Management
Order Management --OrderPlaced--> Inventory
Inventory --StockReserved | StockReservationFailed--> Order Management
```

## Decision

### Client communication

1. External clients call the YARP API Gateway.
2. The Gateway authenticates/routes the request and forwards it to the owning API.
3. The owning API maps HTTP input to a MediatR command or query.
4. Controllers contain transport mapping, not business rules.
5. A long-running asynchronous operation returns `202 Accepted` and exposes a status
   resource that the client can query.

### Service-to-service communication

1. Services communicate business state changes through RabbitMQ integration events
   by default.
2. Events describe facts that already occurred and use past-tense names.
3. A service subscribes using its own durable queue. Producers do not know consumer
   implementations.
4. Services may share only versioned integration-contract and transport-abstraction
   projects. A service must not reference another service's Domain, Application,
   Infrastructure, or API project.
5. A consumer must not call back into the producer to complete the same local
   transaction.

### Synchronous service HTTP exception

Direct service-to-service HTTP is allowed only when an immediate response is a real
business requirement and asynchronous state propagation cannot satisfy it. It
requires a new ADR or an amendment that defines:

- the owning API and contract;
- timeout and total latency budget;
- retry rules limited to safe/idempotent operations;
- circuit-breaker behavior;
- authentication and authorization;
- fallback/degraded behavior;
- tracing and operational ownership.

A synchronous call must not be made while holding a database transaction open.

### Message metadata

Every integration event has an `EventId` and UTC occurrence time. Correlation and
causation identifiers must be propagated when the shared envelope adds them. Logs
and traces must record the message id, event type, queue, and owning service without
logging credentials or sensitive payload fields.

## Alternatives considered

### Direct access to another service's database

Rejected. It bypasses the owning service's invariants and couples deployments and
schema changes.

### Synchronous HTTP for every interaction

Rejected. It creates runtime availability coupling and long failure chains.

### One shared application library containing all service logic

Rejected. Shared business logic becomes a distributed monolith. Only stable
contracts and technical building blocks may be shared.

## Consequences

### Positive

- Services can deploy and recover independently.
- RabbitMQ absorbs temporary consumer outages.
- Producers do not need to know how many consumers exist.
- The architecture test can prevent service-to-service project references.

### Trade-offs

- Data is eventually consistent across services.
- Clients must understand `Pending` states for asynchronous workflows.
- Contract compatibility, idempotency, observability, and dead-letter operations are
  mandatory.

## Enforcement

- YARP routes are defined in API Gateway configuration.
- Architecture tests reject cross-service project references.
- Integration contracts live under `GRD.SpChn.Contracts`.
- RabbitMQ implementations stay behind `GRD.SpChn.EventBus.Abstractions`.
- Any new direct service HTTP call requires architecture review.
