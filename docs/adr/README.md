# Architecture Decision Records

Architecture Decision Records (ADRs) capture decisions that affect more than one
service or are expensive to reverse. They explain the context, the chosen rule, the
alternatives rejected, and how the rule is enforced.

The [event-driven development guide](../event-driven-development.md) explains how to apply
these decisions. If a guide or code example conflicts with an accepted ADR, the ADR
is authoritative until it is superseded.

## Status meanings

| Status | Meaning |
| --- | --- |
| `Proposed` | Written and ready for review; not yet approved. |
| `Accepted` | Approved by the required stakeholders and mandatory for new work. |
| `Superseded` | Replaced by a newer ADR, which must be linked from the old record. |
| `Rejected` | Reviewed but deliberately not adopted. |
| `Deprecated` | Still recorded but should no longer be used for new work. |

Only a team or stakeholder review may move an ADR from `Proposed` to `Accepted`.
Implementation alone is not approval.

## Phase 0 decision set

| ADR | Decision | Status |
| --- | --- | --- |
| [0001](0001-service-communication-boundaries.md) | Service communication boundaries | Proposed |
| [0002](0002-service-owned-databases.md) | Service-owned databases; no shared database access | Proposed |
| [0003](0003-local-transactions-outbox-inbox.md) | Local transactions with Outbox/Inbox; no distributed transactions | Proposed |
| [0004](0004-integration-event-contract-conventions.md) | Integration-event naming and versioning | Proposed |

## Phase 0 Definition of Done

- [x] Client-to-service and service-to-service communication rules are written.
- [x] Database ownership and the no-shared-database rule are written.
- [x] Local transaction boundaries are written.
- [x] The decision against distributed transactions is written.
- [x] Outbox, Inbox, delivery, retry, and acknowledgment semantics are written.
- [x] Integration-event naming and versioning conventions are written.
- [ ] Architecture owner review completed.
- [ ] Service-team representatives reviewed the consequences.
- [ ] Product/technical stakeholder sign-off recorded below.
- [ ] ADR statuses changed from `Proposed` to `Accepted` after approval.

Phase 0 is not complete until the unchecked approval items are completed. Codex does
not infer or fabricate organizational approval.

## Sign-off record

| Role | Name | Decision | Date |
| --- | --- | --- | --- |
| Architecture owner | Pending | Pending | Pending |
| Order Management representative | Pending | Pending | Pending |
| Inventory representative | Pending | Pending | Pending |
| Product/technical stakeholder | Pending | Pending | Pending |

Use `Accept`, `Reject`, or `Request changes` in the Decision column. When all required
reviewers accept, update each ADR's status and add the approval date.

## Explicitly not part of Phase 0

- Creating or renaming projects.
- Adding NuGet packages.
- Creating database tables or migrations.
- Implementing commands, handlers, repositories, consumers, or workers.
- Provisioning MySQL, RabbitMQ, deployment infrastructure, or CI/CD.
- Expanding the v1 order slice with payment, shipment, notification, pricing, or
  partial reservation.

Those activities belong to later development phases and must follow the accepted
ADRs.
