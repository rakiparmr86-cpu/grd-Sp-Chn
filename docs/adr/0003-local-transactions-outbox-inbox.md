# ADR 0003: Local transactions with Outbox and Inbox

- **Status:** Proposed
- **Date:** 2026-08-21
- **Decision owners:** Architecture owner and service-team representatives
- **Scope:** Workflows combining database changes and integration events

## Context

A database commit and a RabbitMQ publish cannot be made atomic with a normal local
transaction. Publishing directly before or after a database save creates a failure
window in which either the event or the business change exists alone.

Coordinating MySQL, PostgreSQL, RabbitMQ, or other service resources through two-phase
commit/distributed transactions would increase coupling and operational risk.

## Decision

1. Distributed transactions and two-phase commit are not used between services,
   databases, or RabbitMQ.
2. Each service uses one local database transaction for its aggregate changes and
   its Outbox/Inbox records.
3. An outbound business change writes its integration event to the service's Outbox
   in the same transaction as the aggregate.
4. The Outbox Publisher reads committed rows, publishes with broker confirms, and
   marks a row processed only after publication succeeds.
5. RabbitMQ delivery is at least once; duplicate publication and delivery are normal
   operating conditions.
6. A consumer inserts `EventId` into its Inbox in the same transaction as all local
   side effects and any response Outbox event.
7. A unique Inbox key makes a duplicate delivery a successful no-op.
8. The consumer acknowledges the RabbitMQ delivery only after its local transaction
   commits.
9. Processing retry is bounded. A terminal technical failure goes to a dead-letter
   queue with the original message identity retained.
10. Expected business outcomes, such as insufficient stock, produce an explicit
    integration event rather than a technical retry.

## V1 transaction boundaries

### Order creation transaction

```text
Order row + Order items + OrderPlaced Outbox row
```

### Inventory reservation transaction

```text
Inventory Inbox row
+ stock updates (all or none)
+ StockReserved or StockReservationFailed Outbox row
```

### Order result transaction

```text
Order Management Inbox row + Order status update
```

No transaction spans more than one service or resource manager.

## Alternatives considered

### Save and then publish directly

Rejected. A crash after commit loses the notification unless a durable Outbox exists.

### Publish and then save

Rejected. Consumers may process an event for a business change that later rolls back.

### Distributed transaction coordinator

Rejected. It couples service availability and deployment to shared coordination,
does not align with RabbitMQ at-least-once delivery, and complicates recovery.

### Exactly-once delivery claim

Rejected. The system provides at-least-once transport plus idempotent effects. It
does not claim global exactly-once execution.

## Consequences

### Positive

- A committed business change cannot lose its publication intent.
- Consumers tolerate duplicate deliveries safely.
- Services remain independently transactional and recoverable.

### Trade-offs

- Cross-service state is eventually consistent.
- Outbox/Inbox tables need cleanup and operational monitoring.
- Operators need dead-letter diagnosis and controlled replay procedures.

## Enforcement

- Transactional commands use the service Unit of Work.
- Repositories never publish messages or commit a private transaction.
- Outbox and Inbox writers require the active Unit of Work connection/transaction.
- Tests cover rollback, duplicate EventId handling, publisher failure, and replay.
- Alerts cover Outbox age/backlog and dead-letter queue depth.
