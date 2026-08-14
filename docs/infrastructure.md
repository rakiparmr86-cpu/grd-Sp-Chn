# GRD infrastructure

## Service composition

Each HTTP service is composed in the same order:

1. `AddServiceDefaults()` configures structured logs, problem details, and health checks.
2. `AddApplication()` discovers MediatR handlers and FluentValidation validators.
3. `AddInfrastructure(configuration)` registers MySQL persistence and RabbitMQ publishing.
4. Controllers, authorization, OpenAPI, and service endpoints are mapped.

The API projects are the composition roots. Domain and Application projects do not
depend on MySQL, RabbitMQ, ASP.NET Core, or other infrastructure implementations.

## Required configuration

Use environment variables in deployed environments. Do not store credentials in
`appsettings.json`.

| Setting | Environment variable | Purpose |
| --- | --- | --- |
| `ConnectionStrings:Database` | `ConnectionStrings__Database` | Service-owned MySQL connection string |
| `RabbitMq:HostName` | `RabbitMq__HostName` | RabbitMQ host |
| `RabbitMq:Port` | `RabbitMq__Port` | AMQP port; defaults to `5672` |
| `RabbitMq:UserName` | `RabbitMq__UserName` | RabbitMQ user |
| `RabbitMq:Password` | `RabbitMq__Password` | RabbitMQ password |
| `RabbitMq:VirtualHost` | `RabbitMq__VirtualHost` | Virtual host; defaults to `/` |
| `RabbitMq:ExchangeName` | `RabbitMq__ExchangeName` | Topic exchange; defaults to `grd.integration` |

`IDbConnectionFactory` opens connections lazily. An API can therefore start without
a database during early development, but its repository operation will fail with a
clear configuration error until `ConnectionStrings__Database` is supplied.

## Local dependencies

Copy `deploy/docker/.env.example` to `deploy/docker/.env`, change the local passwords,
then start MySQL and RabbitMQ from the `deploy/docker` directory:

```text
docker compose --env-file .env -f compose.infrastructure.yml up -d
```

RabbitMQ management is available at `http://localhost:15672` by default. The compose
file is intended only for local development; production secrets must come from the
deployment platform's secret manager.

## Gateway routes

In Development, the gateway listens on `http://localhost:7000` and forwards paths
under `/api/<service>/...` to the ports in each service's launch profile. Production
destinations should be supplied through configuration or environment variables.

Order creation and lookup use public gateway paths without an `/api` prefix:

- `POST http://localhost:7000/orders`
- `GET http://localhost:7000/orders/{orderId}`

## Order and inventory workflow

Order Management and Inventory use local transactions plus the transactional Outbox
and idempotent Inbox patterns. No distributed database transaction is required.

1. `POST /orders` sends `CreateOrderCommand` to Order Management.
2. Order Management inserts the pending order, its lines, and an
   `OrderPlacedIntegrationEvent` into `order_management_outbox` in one transaction.
3. Outbox Publisher sends that row to `order.events` with routing key `order.placed`.
4. Inventory consumes it from the durable `inventory.order-placed` queue. It first
   inserts the event id into `inventory_inbox`, then locks and checks every stock row.
5. Inventory decrements all requested stock or none of it, then inserts either
   `StockReservedIntegrationEvent` or `StockReservationFailedIntegrationEvent` into
   `inventory_outbox` in the same transaction.
6. Outbox Publisher sends the result to `inventory.events`.
7. Order Management consumes the result through a durable result queue, inserts its
   event id into `order_management_inbox`, and updates the order to `Confirmed` or
   `Cancelled` in the same transaction.

RabbitMQ delivery is at least once. A publisher crash between broker publication and
the Outbox update can publish a duplicate; Inbox primary keys make consumer handling
idempotent.

## Local workflow setup

The MySQL initialization script is
`deploy/docker/mysql/init/001_order_inventory_workflow.sql`. Docker executes it only
when creating a fresh MySQL data volume. For an existing volume, apply the script to
the configured database manually before starting the APIs.

Set the following values in each process environment. The example values match
`deploy/docker/.env.example`; use proper secrets outside local development.

```text
ConnectionStrings__Database=Server=localhost;Port=3306;Database=grd_local;User ID=grd;Password=grd-local
RabbitMq__HostName=localhost
RabbitMq__Port=5672
RabbitMq__UserName=grd
RabbitMq__Password=grd-local
```

The Outbox Publisher needs both service connection names. They may point to the same
local database because the tables are service-prefixed; deployed environments can
point them to separate databases.

```text
ConnectionStrings__OrderDatabase=Server=localhost;Port=3306;Database=grd_local;User ID=grd;Password=grd-local
ConnectionStrings__InventoryDatabase=Server=localhost;Port=3306;Database=grd_local;User ID=grd;Password=grd-local
```

Start API Gateway, Order Management API, Inventory API, and Outbox Publisher as four
separate processes. To create a successful reservation, first set stock directly or
through the existing inventory gateway route:

```http
PUT http://localhost:7000/api/inventory/stock/22222222-2222-2222-2222-222222222222
Content-Type: application/json

{
  "availableQuantity": 10
}
```

Then create an order:

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

The POST response is `202 Accepted` with status `Pending`. Poll the returned location;
after asynchronous reservation, `GET /orders/{id}` returns `Confirmed` or `Cancelled`.

## Health checks

- `/health/live` proves that the process is alive.
- `/health/ready` runs every registered readiness check.

Database and broker readiness checks should be added when deployment credentials and
the expected failure policy are defined; liveness must remain independent of external
dependencies.
