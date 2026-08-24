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

### Communication selection rule

Choose the mechanism from the business requirement, not from a preference for HTTP
or messaging:

| Question | Mechanism | Required meaning |
| --- | --- | --- |
| Does the caller require an authoritative answer now to continue? | HTTP query/API | A synchronous response is part of the current use case. |
| Has this service committed a business fact that another service can react to later? | RabbitMQ integration event | The producer is announcing a past-tense fact and accepts eventual consistency. |
| Does the fact matter only inside the current service or aggregate boundary? | Domain event or direct in-process call | The detail remains private and can change with the service implementation. |
| Is the information only needed for a read screen/report? | HTTP query or read model | No business event is introduced merely to transport read data. |
| Is there no identified consumer or business reaction? | No event | Do not publish speculative contracts "just in case." |

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

### Domain events

1. A Domain event belongs to the service that owns the aggregate and is not a
   cross-service contract.
2. It may trigger another in-process handler in the same service when that keeps the
   aggregate/use case cohesive.
3. It must not be serialized directly to RabbitMQ. The Application layer explicitly
   maps an approved Domain fact to an Integration event.
4. If only one local handler needs the information and a direct method call is
   clearer, no Domain event is required.

### When not to create an integration event

Do not create or publish an event when:

- the need is a pure read that belongs in an API query or read model;
- no concrete consuming service and business reaction have been identified;
- the payload exposes a field-level persistence change rather than a meaningful
  business fact;
- the fact is an internal implementation detail of one service;
- eventual consistency is unacceptable to the consuming use case;
- the proposed publisher and consumer are so tightly coupled that the service
  boundary itself should be reconsidered;
- messaging is being used to hide an unclear ownership boundary.

An integration event is not created for every aggregate property update. Publishing
events "just in case" creates contract, deployment, replay, Inbox, observability, and
operational obligations without delivering a business capability. That is a
distributed monolith smell, not loose coupling.

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

An HTTP availability/price lookup can inform an immediate user decision, but it does
not reserve inventory. If the workflow needs an authoritative reservation, the
owning Inventory service must still protect that state change against concurrency.
The caller must also define what happens when the lookup service is slow or
unavailable.

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
- Every proposed integration event identifies its producer, at least one real
  consumer, the consumer's reaction, consistency expectation, owner, and retention/
  replay impact before the contract is added.
- Contract tests enforce the structural event envelope and naming convention.
- Code review rejects speculative, field-level, or consumer-less events.
