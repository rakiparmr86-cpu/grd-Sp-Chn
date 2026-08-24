# Event-driven development standard

This document is the implementation standard for event-driven workflows in GRD
Supply Chain. It applies first to the Order Management and Inventory workflow and
should be followed when adding another service or use case.

The authoritative Phase 0 decisions are indexed in the
[Phase 0 ADR index](adr/README.md). The ADRs are currently `Proposed`; this guide
describes their intended implementation but does not replace stakeholder approval.

## Start here: the architecture in plain language

The main goal is to let each service own its data and continue working independently.
Order Management must not update Inventory tables, and Inventory must not update
Order Management tables. They communicate by publishing facts through RabbitMQ.

For example:

1. The client asks Order Management to create an order.
2. Order Management saves the order as `Pending` and records the fact `OrderPlaced`.
3. A background worker publishes that fact to RabbitMQ.
4. Inventory receives the fact and decides whether stock can be reserved.
5. Inventory publishes either `StockReserved` or `StockReservationFailed`.
6. Order Management receives the result and changes the order to `Confirmed` or
   `Cancelled`.

The client does not wait for all six steps. The initial request returns `202 Accepted`
after step 2. The client then calls `GET /orders/{id}` to see the current status.

### The three rules to remember

1. **A service changes only its own database.** Cross-service communication uses an
   integration event, never another service's repository or tables.
2. **Business data and its Outbox message commit together.** This prevents a saved
   business change from losing the event that other services need.
3. **A consumer records the message in its Inbox before applying side effects.** The
   Inbox and side effects commit together so duplicate RabbitMQ deliveries are safe.

### Why direct RabbitMQ publishing is unsafe

This sequence has a failure gap:

```text
Save Order to MySQL
Process crashes here
Publish OrderPlaced to RabbitMQ
```

The order exists, but Inventory never hears about it. Reversing the order is also
unsafe because RabbitMQ may receive an event for a database transaction that later
fails.

The Outbox removes that gap:

```text
Begin local MySQL transaction
  Save Order
  Save OrderPlaced in Outbox
Commit local MySQL transaction

OutboxPublisher publishes the saved Outbox row later
```

Either both database rows commit or neither commits. RabbitMQ publishing can then be
retried safely without holding the client's HTTP request open.

## Key terms in plain English

| Term | Meaning in this project | Simple example |
| --- | --- | --- |
| Aggregate | A domain object that protects business rules and valid state changes. | `Order` allows `Pending -> Confirmed` but rejects changing an already completed order. |
| Command | A request to change state. | `CreateOrderCommand`, `SetStockCommand`. |
| Query | A request to read state without changing it. | `GetOrderQuery`, `GetStockQuery`. |
| Handler | Application code that performs one command, query, or event use case. | `CreateOrderCommandHandler`. |
| Repository | Loads or persists an aggregate while hiding Dapper/MySQL details. | `IOrderRepository` implemented by `OrderRepository`. |
| Unit of Work | Owns one database transaction shared by repositories, Inbox, and Outbox. | Create Order and its Outbox row commit together. |
| Domain event | An internal fact raised by an aggregate. It belongs to one service. | `OrderCreatedDomainEvent`. |
| Integration event | A stable message contract published for other services. | `OrderPlacedIntegrationEvent`. |
| Outbox | A database table containing messages that still need to be published. | `order_management_outbox`. |
| Inbox | A database table containing message ids already handled by a consumer. | `inventory_inbox`. |
| Idempotent consumer | A consumer that is safe when it receives the same message more than once. | Duplicate `OrderPlaced` does not reserve stock twice. |
| Exchange | RabbitMQ routing point to which producers publish. | `order.events`. |
| Routing key | Message category used to select interested queues. | `order.placed`. |
| Queue | Durable RabbitMQ storage owned by one consumer. | `inventory.order-placed`. |
| Process Manager | Application component that reacts to events and advances a multi-step business process. | `OrderProcessManager` applies the inventory result to Order. |
| Saga | A persisted Process Manager for longer workflows with timeouts and compensation. | Needed later if payment, shipment, and rollback must be coordinated. |
| Result | Success or an expected business error returned as data. | `Result<OrderResponse>` can contain `Orders.NotFound`. |
| Dead-letter queue | Queue holding messages that still failed after bounded retries. | `inventory.order-placed.dead-letter`. |

## Current code map

Use this table when tracing the workflow in the debugger.

| Step | Project/file | Responsibility |
| --- | --- | --- |
| Gateway route | [`ApiGateway/appsettings.Development.json`](../src/backend/ApiGateway/GRD.SpChn.ApiGateway/appsettings.Development.json) | Routes public `/orders` requests to Order Management on port `5255`. |
| HTTP endpoint | [`OrdersController.cs`](../src/backend/Services/OrderManagement/GRD.SpChn.OrderManagement.Api/Controllers/OrdersController.cs) | Converts HTTP input to a command/query and maps `Result<T>` to HTTP. |
| Command | [`CreateOrderCommand.cs`](../src/backend/Services/OrderManagement/GRD.SpChn.OrderManagement.Application/Orders/CreateOrder/CreateOrderCommand.cs) | Defines the data required to create an order and marks it transactional. |
| Command handler | [`CreateOrderCommandHandler.cs`](../src/backend/Services/OrderManagement/GRD.SpChn.OrderManagement.Application/Orders/CreateOrder/CreateOrderCommandHandler.cs) | Creates the Order aggregate, calls the repository, and adds the integration event to Outbox. |
| Order rules | [`Order.cs`](../src/backend/Services/OrderManagement/GRD.SpChn.OrderManagement.Domain/Order.cs) | Owns order creation and status transitions. |
| Order transaction | [`OrderUnitOfWork.cs`](../src/backend/Services/OrderManagement/GRD.SpChn.OrderManagement.Infrastructure/Persistence/OrderUnitOfWork.cs) | Commits or rolls back all Order Management database writes as one operation. |
| Order persistence | [`OrderRepository.cs`](../src/backend/Services/OrderManagement/GRD.SpChn.OrderManagement.Infrastructure/Persistence/OrderRepository.cs) | Reads/writes Order data; contains no RabbitMQ logic. |
| Order Outbox | [`OrderOutboxWriter.cs`](../src/backend/Services/OrderManagement/GRD.SpChn.OrderManagement.Infrastructure/Outbox/OrderOutboxWriter.cs) | Writes outbound Order messages using the active transaction. |
| Publisher worker | [`OutboxPublisher/Worker.cs`](../src/backend/Workers/GRD.SpChn.OutboxPublisher/Worker.cs) | Polls Outbox tables, publishes rows, and marks successful rows processed. |
| RabbitMQ adapter | [`RabbitMqEventBus.cs`](../src/backend/BuildingBlocks/GRD.SpChn.EventBus.RabbitMQ/RabbitMqEventBus.cs) | Hides RabbitMQ publishing behind `IEventBus` and enables publisher confirms. |
| RabbitMQ consumer host | [`RabbitMqConsumerHostedService.cs`](../src/backend/BuildingBlocks/GRD.SpChn.EventBus.RabbitMQ/RabbitMqConsumerHostedService.cs) | Declares topology, deserializes, retries, acknowledges, and dead-letters messages. |
| Inventory event adapter | [`OrderPlacedIntegrationEventHandler.cs`](../src/backend/Services/Inventory/GRD.SpChn.Inventory.Application/IntegrationEvents/OrderPlacedIntegrationEventHandler.cs) | Maps `OrderPlaced` contract data to the internal `ReserveStockCommand`; contains no reservation logic. |
| Inventory reservation command | [`ReserveStock`](../src/backend/Services/Inventory/GRD.SpChn.Inventory.Application/Stock/ReserveStock) | Deduplicates `OrderPlaced`, performs the all-or-nothing reservation, and records the result event through Application ports. |
| Inventory transaction | [`InventoryUnitOfWork.cs`](../src/backend/Services/Inventory/GRD.SpChn.Inventory.Infrastructure/Persistence/InventoryUnitOfWork.cs) | Commits Inventory Inbox, stock changes, and Inventory Outbox together. |
| Inventory rules | [`StockItem.cs`](../src/backend/Services/Inventory/GRD.SpChn.Inventory.Domain/StockItem.cs) | Decides whether stock can be reserved and prevents negative quantity. |
| Inventory Inbox | [`InventoryInboxStore.cs`](../src/backend/Services/Inventory/GRD.SpChn.Inventory.Infrastructure/Inbox/InventoryInboxStore.cs) | Rejects an already processed integration-event id. |
| Inventory Outbox | [`InventoryOutboxWriter.cs`](../src/backend/Services/Inventory/GRD.SpChn.Inventory.Infrastructure/Outbox/InventoryOutboxWriter.cs) | Stores `StockReserved` or `StockReservationFailed`. |
| Final Order step | [`OrderProcessManager.cs`](../src/backend/Services/OrderManagement/GRD.SpChn.OrderManagement.Application/Orders/OrderProcessManager.cs) | Deduplicates the result and confirms or cancels the Order aggregate. |
| Shared contracts | [`IntegrationEvents`](../src/backend/BuildingBlocks/GRD.SpChn.Contracts/IntegrationEvents) | Contains events and RabbitMQ topology names shared between publishers and consumers. |
| MySQL schema | [`001_order_inventory_workflow.sql`](../deploy/docker/mysql/init/001_order_inventory_workflow.sql) | Creates Order, Inventory, Inbox, and Outbox tables. |
| End-to-end smoke test | [`smoke-order-inventory-workflow.ps1`](../scripts/smoke-order-inventory-workflow.ps1) | Proves both `Confirmed` and `Cancelled` branches through the public Gateway. |

## Domain event versus integration event

These are deliberately different:

```text
Order.Create(...)
  -> raises OrderCreatedDomainEvent       (inside Order Management)

CreateOrderCommandHandler
  -> maps it to OrderPlacedIntegrationEvent
  -> writes the integration event to Outbox

OutboxPublisher
  -> publishes OrderPlacedIntegrationEvent (outside Order Management)
```

A domain event may contain service-internal concepts and can change with the domain
model. An integration event is a public contract and must remain compatible with its
consumers. Do not serialize and publish the aggregate or domain event directly.

## Architecture decisions

| Pattern | Location | Decision |
| --- | --- | --- |
| Aggregate | `*.Domain` | Own invariants and state transitions. Do not put SQL, HTTP, or RabbitMQ code in an aggregate. |
| Repository | `*.Application/Abstractions`, implemented in `*.Infrastructure/Persistence` | Repositories load and persist aggregates only. They do not publish events or control transactions. |
| Unit of Work | `*.Application/Abstractions`, implemented in `*.Infrastructure/Persistence` | Defines the atomic boundary around repository, Inbox, and Outbox writes. |
| Mediator | `*.Application` | Controllers create a command/query and call `ISender.Send`. Handlers own use-case orchestration. |
| Outbox | `*.Infrastructure/Outbox` | Stores the intent to publish in the same local transaction as the aggregate change. |
| Inbox | `*.Infrastructure/Inbox` | Uses the integration-event id as a unique key so an at-least-once delivery is processed once. |
| Adapter | `EventBus.RabbitMQ` | Implements transport-neutral EventBus abstractions and contains RabbitMQ topology, acknowledgment, retry, and dead-letter details. |
| Process Manager | `OrderManagement.Application/Orders/OrderProcessManager.cs` | Coordinates reservation-result messages and the Order aggregate. The aggregate remains the persisted process state. |
| Result | `BuildingBlocks/GRD.SpChn.SharedKernel` | Returns validation, not-found, and conflict outcomes without throwing for expected failures. |
| Decorator | `*.Application/Behaviors` | MediatR behaviors apply logging, validation, and transactions around handlers. |
| Specification | `*.Domain` when needed | Add only for a named, reusable business predicate. `StockItem.CanReserve` is currently small enough to remain an aggregate method. |
| Circuit breaker | Future `*.Infrastructure/ExternalServices` | Add a resilience pipeline to outbound HTTP clients. Do not put a circuit breaker around local domain or database operations. |

The current order flow is choreography with a small process manager, not a separate
persisted Saga. Introduce a Saga store when a workflow coordinates several steps such
as payment, inventory, shipment, timeout, and compensation. At that point the Saga
must persist its state, consumed message ids, current step, deadlines, and
compensating actions.

## Dependency rule

Dependencies point inward:

```text
Api -> Application -> Domain
 |          ^
 |          |
 +---- Infrastructure

Infrastructure -> Application abstractions + Domain
EventBus.RabbitMQ -> EventBus.Abstractions
```

- Domain references no Application, Infrastructure, database, broker, or ASP.NET
  package.
- Application owns repository, Unit of Work, Inbox, and Outbox interfaces because
  the use case defines what persistence must do.
- Infrastructure implements those interfaces with Dapper/MySQL and RabbitMQ.
- API is the composition root. It registers Application and Infrastructure but does
  not contain business rules.
- One service must never reference another service's Domain, Application, or
  Infrastructure project. Services share only versioned integration contracts.

## Concrete request flow: API Gateway to final order state

### Synchronous request boundary

```text
Client
  -> POST http://localhost:7000/orders
  -> YARP matches the order-management route
  -> forwards POST /orders to http://localhost:5255
  -> OrdersController creates CreateOrderCommand
  -> MediatR LoggingBehavior
  -> MediatR ValidationBehavior
  -> MediatR TransactionBehavior opens Order Unit of Work
  -> CreateOrderCommandHandler creates the Order aggregate (Pending)
  -> IOrderRepository inserts order and items
  -> IOutboxWriter inserts OrderPlacedIntegrationEvent
  -> Unit of Work commits both writes atomically
  <- 202 Accepted with Pending order and Location header
```

The HTTP request finishes after the local transaction commits. It does not wait for
Inventory. A `202 Accepted` response therefore means the workflow started safely;
it does not mean stock was reserved.

### Asynchronous Order Management to Inventory boundary

```text
OutboxPublisher
  -> polls order_management_outbox
  -> publishes OrderPlacedIntegrationEvent
  -> exchange: order.events
  -> routing key: order.placed
  -> queue: inventory.order-placed
  -> RabbitMqConsumerHostedService deserializes the contract
  -> OrderPlacedIntegrationEventHandler maps the event to ReserveStockCommand
  -> MediatR TransactionBehavior opens Inventory Unit of Work
  -> ReserveStockCommandHandler orchestrates the reservation
  -> IInboxStore INSERT IGNORE by EventId
       duplicate -> commit/ack without applying stock again
       new       -> continue
  -> IInventoryRepository locks each stock row FOR UPDATE
  -> StockItem.CanReserve checks every requested quantity
  -> success: reserve and update every StockItem
     failure: do not reduce any stock
  -> IOutboxWriter inserts StockReserved or StockReservationFailed
  -> Unit of Work commits Inbox + stock + Outbox atomically
  <- consumer acknowledges the RabbitMQ delivery
```

Inventory never calls Order Management over HTTP for this workflow. Its reply is an
integration event, so each service remains available independently.

### Asynchronous Inventory to Order Management boundary

```text
OutboxPublisher
  -> polls inventory_outbox
  -> publishes to inventory.events
  -> routing key: inventory.stock-reserved
     or inventory.stock-reservation-failed
  -> Order Management durable consumer queue
  -> StockReserved/StockReservationFailed handler
  -> OrderProcessManager opens Order Unit of Work
  -> IInboxStore deduplicates EventId
  -> IOrderRepository locks and loads the Order aggregate
  -> Order.Confirm() or Order.Cancel()
  -> IOrderRepository updates status
  -> Unit of Work commits Inbox + aggregate atomically
  <- consumer acknowledges delivery

Client
  -> GET http://localhost:7000/orders/{orderId}
  <- Pending, Confirmed, or Cancelled
```

## Transaction and delivery guarantees

There is no distributed transaction across Order Management, RabbitMQ, and
Inventory. Each service has one local transaction:

1. Order + Order Outbox.
2. Inventory Inbox + stock changes + Inventory Outbox.
3. Order Inbox + final Order state.

RabbitMQ delivery is at least once. The publisher can send a message and fail before
marking its Outbox row processed, so duplicates are expected. Inbox uniqueness makes
consumer side effects idempotent.

Consumer processing uses a bounded exponential retry. After the configured number
of attempts, the RabbitMQ adapter publishes the original message to a durable
`<exchange>.dead-letter` exchange and `<queue>.dead-letter` queue. If dead-letter
publication fails, the original delivery is requeued so it is not lost.

Never acknowledge a message before its local transaction commits. Publisher confirms
must be enabled, and an Outbox row must never be marked processed before the broker
confirms publication.

### What happens when something fails?

| Situation | Expected behavior | Why data remains safe |
| --- | --- | --- |
| Create Order validation fails | API returns `400`; handler and transaction do not run. | No database state was changed. |
| Order database insert fails | Unit of Work rolls back Order and Outbox. API returns an unexpected-error response. | There cannot be an Outbox message without its Order. |
| RabbitMQ is unavailable | Order stays `Pending`; Outbox row remains unprocessed and is retried. | The intent to publish is durable in MySQL. |
| Publisher sends an event twice | Consumer receives the same `EventId` twice. | Inbox unique key makes the second delivery a no-op. |
| Inventory has insufficient stock | Inventory commits its Inbox plus `StockReservationFailed` Outbox row. | This is a business outcome, not a technical exception; Order later becomes `Cancelled`. |
| Inventory handler throws temporarily | RabbitMQ adapter retries with exponential delay. | The failed local transaction rolls back before every retry. |
| Handler still fails after all attempts | Original payload is moved to the consumer's dead-letter queue. | The bad message is retained for diagnosis and controlled replay. |
| Order result event arrives twice | Order Management Inbox ignores the duplicate. | `Confirm` or `Cancel` is not applied twice. |
| Client reads immediately after POST | `GET` may return `Pending`. | Asynchronous processing has not necessarily finished; this is normal. |

### How to know that processing finished

The `POST /orders` response contains the new order id and a `Location` header. Poll:

```http
GET http://localhost:7000/orders/{orderId}
```

Interpret the status as follows:

| Status | Meaning | Client action |
| --- | --- | --- |
| `Pending` | Order is saved; inventory result has not yet been applied. | Wait briefly and query again. |
| `Confirmed` | Inventory reserved all requested stock. | Continue the order workflow. |
| `Cancelled` | Inventory could not reserve at least one item. | Show the failure outcome or request different quantities. |

`Pending` is not automatically an error. Alert only when it remains pending longer
than the agreed workflow service-level objective; inspect Outbox backlog, consumer
logs, and dead-letter queues in that order.

## Professional development sequence for a new use case

### 1. Define the business capability and owner

- Write the command, actor, preconditions, invariant, expected result, and failure
  outcomes.
- Select exactly one service as the owner of each aggregate and database table.
- Decide which facts other services need. Publish facts that happened, not remote
  commands disguised as events.
- Decide whether the caller needs an immediate result (`200/201`) or asynchronous
  acceptance (`202` plus a status resource).

Example: Order Management owns Order. Inventory owns StockItem. `OrderPlaced` is a
fact; Inventory decides whether stock can be reserved.

### 2. Model the Domain first

In the service's `.Domain` project:

- Create the aggregate root, entities, value objects, and domain events.
- Put all state changes behind intention-revealing methods such as `Confirm`,
  `Cancel`, or `Reserve`.
- Reject invalid transitions inside the aggregate even if API validation also
  exists.
- Add unit tests for invariants and state transitions before persistence code.

Do not start with controllers or database tables. The model should be testable with
no container, network, or database.

### 3. Define Application commands and queries

In `.Application`:

- Create one MediatR request per use case.
- Commands change state; queries only read.
- Add FluentValidation validators for input shape and cheap boundary rules.
- Return `Result<T>` for expected validation, not-found, and conflict outcomes.
- Mark commands that require atomic persistence with `ITransactionalRequest`.
- Keep handlers focused on orchestration: load aggregate, call domain behavior,
  persist aggregate, add Outbox intent.

The MediatR behaviors execute in this order:

1. Logging.
2. Validation.
3. Transaction.
4. Handler.

This ensures invalid commands do not open a database transaction.

### 4. Define Application-owned ports

Add only interfaces required by the use case:

- `I<Aggregate>Repository` for aggregate persistence.
- `IUnitOfWork` for the atomic boundary.
- `IOutboxWriter` when a committed change must publish an event.
- `IInboxStore` for an inbound integration-event handler.
- An external service interface when the use case needs HTTP or another provider.

Do not return Dapper rows, EF entities, RabbitMQ types, or HTTP response types from an
Application interface.

### 5. Define integration contracts

In `BuildingBlocks/GRD.SpChn.Contracts`:

- Use past-tense names such as `OrderPlacedIntegrationEvent`.
- Include a globally unique `EventId` and UTC occurrence time.
- Include stable business identifiers and only the data consumers require.
- Treat a published contract as public. Prefer additive changes.
- Introduce a new event version for breaking schema or semantic changes.
- Define exchange and routing-key names in `MessagingTopology`.

A domain event is internal to one service. Map it to an integration event at the
Application boundary; never publish a Domain object directly.

### 6. Implement Infrastructure adapters

In `.Infrastructure`:

- `Persistence`: implement the repository and Unit of Work.
- `Outbox`: serialize and insert integration events using the active Unit of Work.
- `Inbox`: insert the message id using the active Unit of Work.
- `ExternalServices`: implement outbound HTTP/provider ports with typed clients,
  timeouts, retry for transient failures, and a circuit breaker.
- Register all implementations in `DependencyInjection.cs`.

Repository methods must use the active Unit of Work for writes. A handler that calls
repository write + Outbox write must be wrapped by one Unit of Work; neither component
may open and commit its own independent transaction.

### 7. Add database migration/schema

- Create service-owned aggregate tables and indexes.
- Create an Outbox table with unique `event_id`, available time, processed time,
  retry count, and last error.
- Create an Inbox table with `event_id` as its primary/unique key.
- Add concurrency control: row locks or optimistic version columns according to the
  aggregate's contention profile.
- Make migration application an explicit deployment step; do not rely on local
  Docker initialization scripts in production.

### 8. Add the consumer

- Subscribe with a durable queue owned by the consuming service.
- Deserialize into a versioned integration contract.
- Start the Unit of Work.
- Insert Inbox id before side effects.
- Load/lock the aggregate, call domain behavior, persist changes, and add any new
  Outbox events.
- Commit, then acknowledge.
- On transient failure, retry a bounded number of times.
- On terminal failure, dead-letter with the original message id and diagnostic
  headers.

Consumer logic must be safe when the same event is delivered repeatedly.

### 9. Add or update the YARP route

- Expose only client-facing HTTP endpoints through the gateway.
- Keep broker exchanges and internal worker endpoints private.
- Preserve or deliberately transform the path.
- Apply authentication, authorization, rate limiting, correlation-id propagation,
  and request-size limits at the appropriate boundary.
- Add a gateway smoke test that verifies the destination and transformed path.

The current order route preserves `/orders` and forwards it from port `7000` to Order
Management on port `5255`.

### 10. Add tests in layers

- Domain unit tests: invariants, calculations, and transitions.
- Application unit tests: handler orchestration with fake ports, validation, Result
  mapping, and Process Manager decisions.
- Infrastructure integration tests: real MySQL transaction rollback, Inbox dedupe,
  Outbox atomicity, and row locking.
- Contract tests: serialization compatibility and required fields.
- Broker integration tests: topology, retry, dead-letter, and redelivery.
- End-to-end test: gateway request reaches a final queryable state.
- Architecture tests: Domain/Application dependency rules and no service-to-service
  project references.

### 11. Add operations before release

- Propagate `traceparent`, correlation id, causation id, and message id.
- Record command duration, Outbox backlog age/count, consumer failures, retries,
  dead-letter count, and end-to-end workflow latency.
- Provide readiness checks for required dependencies while keeping liveness local to
  the process.
- Alert on old Outbox rows and any dead-letter messages.
- Provide a controlled replay tool that preserves EventId so Inbox dedupe remains
  effective, or explicitly creates a new EventId for an authorized reprocessing.
- Document data retention for Inbox, Outbox, and dead-letter queues.

## When to add a persisted Saga

Use the existing lightweight `OrderProcessManager` while the workflow is:

```text
OrderPlaced -> StockReserved | StockReservationFailed -> final Order state
```

Add a persisted Saga when any of these become true:

- three or more services must complete before the workflow finishes;
- a step has a business deadline or timeout;
- a completed step needs compensation;
- events can arrive in several valid orders;
- operators need to pause, resume, or inspect workflow progress;
- the Order aggregate alone no longer contains enough orchestration state.

The Saga should reference aggregates by id. It must not directly update another
service's database.

## Outbound HTTP resilience standard

When a service must call an external HTTP API, place the adapter in
`.Infrastructure/ExternalServices` and configure a resilience pipeline:

- strict per-attempt timeout;
- retry only transient, idempotent operations with jitter;
- circuit breaker based on a meaningful sampling window;
- total request timeout/budget;
- no retry for validation, authentication, authorization, or other permanent 4xx
  failures;
- idempotency key for retryable writes.

Do not retry blindly at the controller, handler, repository, and HTTP client layers;
stacked retries multiply load and latency.

## Pull-request checklist

- [ ] Aggregate owns its invariants and transitions.
- [ ] API calls MediatR and contains no business rule.
- [ ] Application owns persistence/external-service interfaces.
- [ ] Repository contains no broker logic and does not commit a private transaction.
- [ ] Aggregate change and Outbox intent share one Unit of Work.
- [ ] Consumer Inbox, aggregate change, and response Outbox share one Unit of Work.
- [ ] Integration contract is version-compatible and contains EventId.
- [ ] Consumer is idempotent and acknowledges only after commit.
- [ ] Retry is bounded and terminal failures reach a dead-letter queue.
- [ ] Expected failures use Result; unexpected failures are logged and allowed to
      trigger infrastructure failure handling.
- [ ] Domain, application, infrastructure, contract, and end-to-end tests are added
      at the appropriate level.
- [ ] Logs, traces, metrics, health behavior, configuration, and runbook are updated.
