# GRD.SpChn.Contracts

Shared Integration-event contracts for GRD Supply Chain services.

## Current Order/Inventory contracts

- `OrderPlacedIntegrationEvent`
- `StockReservedIntegrationEvent`
- `StockReservationFailedIntegrationEvent`

Each event carries `SchemaVersion`, `EventId`, and `OccurredOnUtc` through the shared
`IntegrationEvent` envelope. The current unversioned CLR type and routing key are
wire schema version 1.

## Versioning

The NuGet package uses Semantic Versioning. Compatible additive JSON fields may stay
on the current wire schema when they are optional or have safe defaults. A breaking
message change requires a new CLR type and routing key, such as
`OrderPlacedIntegrationEventV2` and `order.placed.v2`, while the old version remains
available during consumer migration.

See `docs/adr/0004-integration-event-contract-conventions.md` and
`docs/development-phases/phase-6-integration-event-contracts.md` in the source
repository for the complete compatibility policy.
