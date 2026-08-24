# ADR 0002: Service-owned databases

- **Status:** Proposed
- **Date:** 2026-08-21
- **Decision owners:** Architecture owner and data/service owners
- **Scope:** Transactional and read-model data owned by GRD Supply Chain services

## Context

Several services need related data, but allowing them to share tables makes ownership
unclear. A consumer could bypass domain rules, a schema deployment could break
another service, and independent recovery would become impossible.

Local development currently permits Order Management and Inventory tables in one
physical MySQL database for convenience. Table prefixes alone do not grant ownership
to another service.

## Decision

1. Every table, collection, and read model has exactly one owning service.
2. Only the owning service's runtime identity may write its data.
3. A service must not connect to or query another service's database, including for
   reporting, validation, joins, or operational shortcuts.
4. Cross-service data is copied through integration events into a consumer-owned read
   model when required.
5. Database migrations, backups, retention, recovery objectives, and access control
   belong to the owning service.
6. Each production service receives its own logical database and least-privilege
   credentials. Separate physical servers are optional and depend on scale,
   availability, security, and operational requirements.
7. Local development may use one physical MySQL container and one database only when
   schemas/tables are clearly service-prefixed and application connection boundaries
   remain intact.
8. A service may select MySQL, PostgreSQL, or another approved store based on its own
   requirements. This does not permit a cross-database transaction between services.

For the v1 vertical slice:

| Data | Owner |
| --- | --- |
| Orders and order items | Order Management |
| Order Inbox and Outbox | Order Management |
| Stock quantities | Inventory |
| Inventory Inbox and Outbox | Inventory |

## Alternatives considered

### One shared enterprise database

Rejected. It enables cross-service joins and writes, couples migrations, and creates
a shared scaling and failure boundary.

### Read-only access to another service's database

Rejected as the default. Read-only access still couples consumers to an internal
schema and bypasses contract versioning. Use an event-built read model instead.

### Separate physical server for every service from day one

Not required. Logical isolation and ownership are mandatory; physical isolation can
be introduced according to operational needs.

## Consequences

### Positive

- Data ownership and write authority are unambiguous.
- Services can evolve schemas and select database technology independently.
- Failures, backup, restore, and scaling can be isolated.

### Trade-offs

- Cross-service joins are not available at request time.
- Read models contain duplicated data and are eventually consistent.
- Reporting projections need replay, reconciliation, and freshness monitoring.

## Enforcement

- Connection strings are service-specific in production.
- Database users receive permissions only for their service database.
- Code review rejects SQL that targets another service's tables.
- Reporting consumes events or approved export pipelines.
- Integration tests verify that consumers update only their owned schema.
