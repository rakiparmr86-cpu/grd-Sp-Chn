# Phase 4 - Commands and Queries

## Phase outcome

The Application layer now expresses the vertical slice as testable use cases:

- `CreateOrderCommand` creates a `Pending` Order and records the intent to publish
  `OrderPlaced` through an Application-owned Outbox port.
- `ReserveStockCommand` performs an all-or-nothing inventory decision, deduplicates
  the incoming event through an Inbox port, and records either `StockReserved` or
  `StockReservationFailed` through an Outbox port.
- `GetOrderQuery` reads the current order status without opening a transaction.
- HTTP and RabbitMQ adapters translate input into commands/queries; they do not own
  business decisions.

The repository already contains later-phase Infrastructure adapters. Phase 4's
acceptance boundary remains isolated: the Application projects do not reference
Infrastructure, and the handler tests use in-memory implementations of Application
ports without MySQL, RabbitMQ, Docker, or HTTP.

## Objective -> Design -> Implement -> Test -> Deliverable -> DoD

### Objective

Let the Application layer orchestrate Domain behavior while staying thin and
independent of delivery and persistence technology.

### Design

| Use case | Type | Initiated by | Changes state? | Transaction? |
| --- | --- | --- | --- | --- |
| `SetStockCommand` | Command | Warehouse user through Inventory HTTP API | Yes | Yes |
| `CreateOrderCommand` | Command | Order-entry user through Gateway | Yes | Yes |
| `ReserveStockCommand` | Command | `OrderPlaced` RabbitMQ adapter | Yes | Yes |
| `GetOrderQuery` | Query | User/system through Gateway | No | No |
| `GetStockQuery` | Query | Warehouse user/system through Gateway | No | No |

Commands and queries are MediatR requests. The pipeline order is:

```text
Logging -> Validation -> Transaction (commands only) -> Handler
```

Validation runs before the transaction, so invalid user data never reaches a
repository, Inbox, Outbox, or database transaction. A query is not marked with
`ITransactionalRequest`, so it goes directly to its handler after validation.

### Implemented code

| Location | Responsibility |
| --- | --- |
| `OrderManagement.Application/Orders/CreateOrder` | Command, validator, and handler for order creation. |
| `OrderManagement.Application/Orders/GetOrder` | Query and handler for current order state. |
| `Inventory.Application/Stock/ReserveStock` | Command, validator, response, outcome, and handler for reservation. |
| `Inventory.Application/IntegrationEvents/OrderPlacedIntegrationEventHandler.cs` | Thin adapter from the shared event contract to `ReserveStockCommand`. |
| Each Application project's `Abstractions` folder | Repository, Unit of Work, Inbox, and Outbox ports required by handlers. |
| Each Application project's `Behaviors` folder | Logging, validation, and transaction decorators around handlers. |

The handlers depend on interfaces owned by Application:

```text
CreateOrderCommandHandler
  -> Order.Create(...)
  -> IOrderRepository.AddAsync(...)
  -> IOutboxWriter.AddAsync(OrderPlaced...)

ReserveStockCommandHandler
  -> IInboxStore.TryAddAsync(EventId)
  -> IInventoryRepository.GetByProductIdForUpdateAsync(...)
  -> StockItem.CanReserve(...) / StockItem.Reserve(...)
  -> IInventoryRepository.UpdateAsync(...)
  -> IOutboxWriter.AddAsync(StockReserved | StockReservationFailed)

GetOrderQueryHandler
  -> IOrderRepository.GetByIdAsync(...)
```

`TransactionBehavior` supplies one Unit of Work around each transactional command.
That boundary will make the repository write and Outbox write atomic when the real
Infrastructure adapter is connected. The handler never calls `Commit`, SQL, or
RabbitMQ directly.

### Tests

`ApplicationHandlerTests.cs` executes the real MediatR pipeline with in-memory port
implementations. It verifies:

1. Creating an order stores a `Pending` aggregate and one `OrderPlaced` Outbox intent.
2. Invalid order input stops before the transaction and handler.
3. Querying an order reads its status without opening a transaction.
4. Available stock is reduced and a `StockReserved` Outbox intent is written.
5. A multi-item reservation is rejected without partially reducing another item.
6. Replaying the same `EventId` does not reserve or publish twice.
7. Invalid reservation input stops before the transaction, Inbox, repository, and
   Outbox ports.

Run the Phase 4 tests from the repository root:

```powershell
dotnet test tests\backend\GRD.SpChn.UnitTests\GRD.SpChn.UnitTests.csproj
```

These are application tests, not infrastructure integration tests. The same unit
test assembly also contains separate building-block tests; Phase 4's handler tests
do not resolve an Infrastructure implementation.

### Deliverable

The order creation, stock reservation, and order-status query use cases are fully
executable in isolation through MediatR and Application-owned ports.

### Definition of Done

- [x] `CreateOrderCommand` handler calls Domain behavior and only Application ports.
- [x] `ReserveStockCommand` handler calls Domain behavior and only Application ports.
- [x] `GetOrderQuery` is separated from write commands and does not open a Unit of
      Work.
- [x] FluentValidation rejects malformed data before the handler and transaction.
- [x] Repository, Inbox, Outbox, and Unit of Work interfaces are owned by
      Application.
- [x] The RabbitMQ integration-event handler is a thin translation adapter.
- [x] Handler unit tests cover success, rejection, invalid input, idempotency, and
      no-partial-update behavior.
- [x] Application projects contain no Infrastructure project reference.

### Explicitly not required by the Phase 4 test boundary

- A running MySQL database or a concrete Dapper repository.
- A running RabbitMQ broker or actual event publication.
- Real Inbox/Outbox tables or an Outbox polling worker.
- YARP, HTTP controllers, Docker, or an end-to-end environment.
- Payment, shipment, notification, partial reservation, or pricing workflows.

Those concerns belong to later infrastructure, messaging, and end-to-end phases.
Their presence elsewhere in this repository does not make them dependencies of the
Application layer.

## Real-world module flow: a user adds data

### Actors

| Actor | Module used | Goal |
| --- | --- | --- |
| Warehouse operator | Inventory | Record how many units are available for a product. |
| Order-entry user or storefront | Order Management through API Gateway | Place an order containing one or more products. |
| Order viewer or storefront | Order Management query through API Gateway | Show whether the asynchronous order is pending, confirmed, or cancelled. |
| Inventory event consumer | Inventory Application | Convert `OrderPlaced` into an idempotent reservation command. |

### Step 1: warehouse operator adds stock data

Assume the product already has this stable identifier:

```text
22222222-2222-2222-2222-222222222222
```

The operator enters `10` available units in an inventory screen. The screen sends:

```http
PUT http://localhost:7000/api/inventory/stock/22222222-2222-2222-2222-222222222222
Content-Type: application/json

{
  "availableQuantity": 10
}
```

Application flow:

```text
Warehouse user
  -> API Gateway
  -> StockController
  -> SetStockCommand(productId, 10)
  -> ValidationBehavior
  -> TransactionBehavior
  -> SetStockCommandHandler
  -> IInventoryRepository.UpsertAsync(StockItem)
```

The user can verify the value with:

```http
GET http://localhost:7000/api/inventory/stock/22222222-2222-2222-2222-222222222222
```

### Step 2: order-entry user submits order data

The user selects two units and checks out. The client sends:

```http
POST http://localhost:7000/orders
Content-Type: application/json

{
  "customerId": "11111111-1111-1111-1111-111111111111",
  "items": [
    {
      "productId": "22222222-2222-2222-2222-222222222222",
      "quantity": 2
    }
  ]
}
```

Application flow:

```text
Order user
  -> API Gateway
  -> OrdersController maps JSON to CreateOrderCommand
  -> ValidationBehavior rejects bad ids, empty items, non-positive quantity,
     or duplicate products
  -> TransactionBehavior
  -> CreateOrderCommandHandler
  -> Order.Create(...) creates Pending aggregate
  -> IOrderRepository.AddAsync(order)
  -> IOutboxWriter.AddAsync(OrderPlaced)
  <- 202 Accepted with order id, status Pending, and Location header
```

`Pending` is the correct immediate result. It means the order was accepted into an
asynchronous workflow; it does not yet promise that stock exists.

### Step 3: the system handles the module-to-module command

RabbitMQ later delivers `OrderPlacedIntegrationEvent` to Inventory. A user does not
call `ReserveStockCommand` directly. The event adapter maps public contract data to
the internal use case:

```text
OrderPlacedIntegrationEvent
  -> OrderPlacedIntegrationEventHandler (transport adapter)
  -> ReserveStockCommand(EventId, OrderId, Items)
  -> ValidationBehavior
  -> TransactionBehavior
  -> ReserveStockCommandHandler
```

The handler first records `EventId` through `IInboxStore`. For a new message it locks
and checks every requested stock item before reducing any quantity.

- If every item is available, it reserves every item and writes `StockReserved` to
  the Inventory Outbox.
- If any item is missing or insufficient, it changes no stock and writes
  `StockReservationFailed` to the Inventory Outbox.
- If the `EventId` is already in Inbox, it returns `Duplicate` and performs no stock
  or Outbox write.

Inbox, stock changes, and the response Outbox intent share the same transaction when
the Infrastructure Unit of Work is connected.

### Step 4: the user sees the final state

The Inventory result event is consumed by Order Management, which confirms or
cancels the Order. The UI polls the resource identified by the `Location` header:

```http
GET http://localhost:7000/orders/{orderId}
```

| Returned status | Real-world meaning | UI behavior |
| --- | --- | --- |
| `Pending` | Reservation has not completed yet. | Keep the order screen in processing state and poll again with a delay. |
| `Confirmed` | Every requested stock item was reserved. | Show order confirmed and continue to the next future workflow. |
| `Cancelled` | At least one requested item could not be reserved. | Explain that stock was unavailable and let the user adjust the order. |

## Acceptance scenarios

### Scenario A - successful single-item order

```text
Given product A has 10 units
When a user orders 2 units of product A
Then CreateOrder returns Pending
And Inventory changes product A from 10 to 8
And Inventory records StockReserved
And the Order eventually becomes Confirmed
```

### Scenario B - all-or-nothing multi-item failure

```text
Given product A has 10 units and product B has 1 unit
When a user orders 2 units of A and 5 units of B
Then Inventory records StockReservationFailed
And product A remains 10
And product B remains 1
And the Order eventually becomes Cancelled
```

This is not partial reservation. Every requested item succeeds, or none are changed.

### Scenario C - missing stock record

```text
Given the user orders a product that has no Inventory stock record
When ReserveStockCommand checks the product
Then it writes StockReservationFailed with a missing-record reason
And the Order eventually becomes Cancelled
```

### Scenario D - duplicate broker delivery

```text
Given an OrderPlaced EventId was already processed
When RabbitMQ redelivers the same EventId
Then Inbox identifies it as Duplicate
And quantity is not reduced again
And no duplicate result event is added to Outbox
```

### Scenario E - invalid user data

```text
Given customerId is empty, items are empty, quantity is zero, or a product is repeated
When the user submits POST /orders
Then validation returns HTTP 400
And no transaction, repository write, or Outbox write occurs
```

### Scenario F - immediate status query

```text
Given POST /orders returned 202 Accepted
When the user queries the order before Inventory finishes
Then GET /orders/{id} can return Pending
And Pending is treated as normal asynchronous progress, not as failure
```

## Rules for extending Phase 4

When another module is added:

1. Name the user or system actor and describe the data they provide.
2. Add one command for one state-changing use case or one query for one read use case.
3. Keep HTTP/message conversion in an adapter and orchestration in the handler.
4. Put invariants and state transitions in the Domain aggregate.
5. Define the smallest Application-owned ports required by the handler.
6. Mark an atomic command with `ITransactionalRequest`.
7. Return `Result<T>` for expected validation/not-found/conflict outcomes; throw only
   for unexpected technical failures.
8. Test the use case with fake ports before implementing a database or broker adapter.
9. For inbound messages, include Inbox dedupe in the same transaction as side effects.
10. For outbound facts, include the Outbox intent in the same transaction as the
    aggregate change.
