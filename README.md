# GRD Supply Chain

GRD Supply Chain is a .NET 10 microservice solution. The implemented order workflow
uses MySQL local transactions, transactional Outbox/Inbox messaging, RabbitMQ, and a
YARP API Gateway.

## Order workflow

```text
POST /orders
  -> Order Management creates a Pending order and Order Outbox row
  -> Outbox Publisher publishes OrderPlaced to order.events
  -> Inventory deduplicates through Inbox and attempts reservation
  -> Inventory writes StockReserved or StockReservationFailed to its Outbox
  -> Outbox Publisher publishes the result to inventory.events
  -> Order Management deduplicates the result and sets Confirmed or Cancelled
  -> GET /orders/{id} returns the final state
```

## Prerequisites

- .NET SDK 10
- Docker Desktop with Docker Compose
- PowerShell

Run all commands below from the repository root:

```powershell
Set-Location D:\newdata\grd-Sp-Chn
```

## 1. Start MySQL and RabbitMQ

Create the local Docker environment file:

```powershell
Copy-Item deploy\docker\.env.example deploy\docker\.env
```

Review the local passwords in `deploy/docker/.env`, then start the infrastructure:

```powershell
docker compose --env-file deploy\docker\.env `
  -f deploy\docker\compose.infrastructure.yml up -d
```

If another project already uses MySQL port `3306`, set `MYSQL_PORT=3308` in
`deploy/docker/.env`. Then use `Port=3308` in every workflow connection string below.
This changes only the host port; MySQL still listens on `3306` inside its container.

For a fresh MySQL volume, Docker automatically executes:

```text
deploy/docker/mysql/init/001_order_inventory_workflow.sql
```

MySQL initialization scripts run only when the data volume is first created. If the
`grd-spchn_mysql-data` volume already exists, apply the SQL file to `grd_local`
manually using your MySQL client. Do not delete an existing volume unless its data is
no longer required.

RabbitMQ management is available at `http://localhost:15672`.

## 2. Start API Gateway

Open terminal 1:

```powershell
dotnet run --project `
  src\backend\ApiGateway\GRD.SpChn.ApiGateway\GRD.SpChn.ApiGateway.csproj
```

Gateway address: `http://localhost:7000`

## 3. Start Order Management

Open terminal 2 and configure the Order Management database and RabbitMQ connection:

```powershell
$env:ConnectionStrings__Database = "Server=localhost;Port=3306;Database=grd_local;User ID=grd;Password=grd-local"
$env:RabbitMq__HostName = "localhost"
$env:RabbitMq__Port = "5672"
$env:RabbitMq__UserName = "grd"
$env:RabbitMq__Password = "grd-local"

dotnet run --project `
  src\backend\Services\OrderManagement\GRD.SpChn.OrderManagement.Api\GRD.SpChn.OrderManagement.Api.csproj
```

Order Management address: `http://localhost:5255`

## 4. Start Inventory

Open terminal 3 and configure the Inventory database and RabbitMQ connection:

```powershell
$env:ConnectionStrings__Database = "Server=localhost;Port=3306;Database=grd_local;User ID=grd;Password=grd-local"
$env:RabbitMq__HostName = "localhost"
$env:RabbitMq__Port = "5672"
$env:RabbitMq__UserName = "grd"
$env:RabbitMq__Password = "grd-local"

dotnet run --project `
  src\backend\Services\Inventory\GRD.SpChn.Inventory.Api\GRD.SpChn.Inventory.Api.csproj
```

Inventory address: `http://localhost:5018`

## 5. Start Outbox Publisher

Open terminal 4. The worker needs both connection-string names because it polls both
service-owned Outbox tables:

```powershell
$env:ConnectionStrings__OrderDatabase = "Server=localhost;Port=3306;Database=grd_local;User ID=grd;Password=grd-local"
$env:ConnectionStrings__InventoryDatabase = "Server=localhost;Port=3306;Database=grd_local;User ID=grd;Password=grd-local"
$env:RabbitMq__HostName = "localhost"
$env:RabbitMq__Port = "5672"
$env:RabbitMq__UserName = "grd"
$env:RabbitMq__Password = "grd-local"

dotnet run --project `
  src\backend\Workers\GRD.SpChn.OutboxPublisher\GRD.SpChn.OutboxPublisher.csproj
```

Keep all four terminals open. Each `dotnet run` command owns one foreground process;
reusing or closing its terminal stops that service.

## 6. Add available stock

Use a stable product id for the stock and order requests:

```powershell
$productId = "22222222-2222-2222-2222-222222222222"
$stockBody = @{ availableQuantity = 10 } | ConvertTo-Json

Invoke-RestMethod `
  -Method Put `
  -Uri "http://localhost:7000/api/inventory/stock/$productId" `
  -ContentType "application/json" `
  -Body $stockBody
```

## 7. Create an order

```powershell
$orderBody = @{
  customerId = "11111111-1111-1111-1111-111111111111"
  items = @(
    @{
      productId = $productId
      quantity = 2
    }
  )
} | ConvertTo-Json -Depth 4

$order = Invoke-RestMethod `
  -Method Post `
  -Uri "http://localhost:7000/orders" `
  -ContentType "application/json" `
  -Body $orderBody

$order
```

The response is `202 Accepted`, and the initial order status is `Pending`.

## 8. Query the final order status

The workflow is asynchronous. Poll the order until its status becomes `Confirmed` or
`Cancelled`:

```powershell
Invoke-RestMethod -Uri "http://localhost:7000/orders/$($order.id)"
```

- `Confirmed`: Inventory reserved every requested item.
- `Cancelled`: At least one product was missing or had insufficient stock.

## 9. Run the complete workflow smoke test

After Gateway, Order Management, Inventory, and Outbox Publisher are running, execute:

```powershell
pwsh -NoProfile -File scripts\smoke-order-inventory-workflow.ps1
```

The script sends every request through the Gateway. It verifies both branches:

1. Stock is set to `10`, an order requests `2`, and the order becomes `Confirmed`.
2. Another order requests `1000`, reservation fails, and the order becomes `Cancelled`.
3. Final stock remains `8`, proving the failed reservation made no partial change.

Use a different Gateway address or timeout when needed:

```powershell
pwsh -NoProfile -File scripts\smoke-order-inventory-workflow.ps1 `
  -GatewayUrl http://localhost:7000 `
  -TimeoutSeconds 60
```

## Exchanges and queues

| Message | Exchange | Routing key | Consumer queue |
| --- | --- | --- | --- |
| `OrderPlacedIntegrationEvent` | `order.events` | `order.placed` | `inventory.order-placed` |
| `StockReservedIntegrationEvent` | `inventory.events` | `inventory.stock-reserved` | `order-management.stock-reserved` |
| `StockReservationFailedIntegrationEvent` | `inventory.events` | `inventory.stock-reservation-failed` | `order-management.stock-reservation-failed` |

## Build and test

```powershell
dotnet build GRD.SpChn.sln
dotnet test GRD.SpChn.sln --no-build
```

For schema details, environment-variable reference, health endpoints, and delivery
semantics, see [docs/infrastructure.md](docs/infrastructure.md).

For the required design patterns, layer ownership, development sequence, concrete
Gateway-to-service communication flow, transaction boundaries, retry/dead-letter
policy, and pull-request checklist, see
[docs/event-driven-development.md](docs/event-driven-development.md).
