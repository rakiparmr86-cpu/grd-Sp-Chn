[CmdletBinding()]
param(
    [string]$GatewayUrl = "http://localhost:7000",
    [int]$TimeoutSeconds = 30
)

$ErrorActionPreference = "Stop"
$gateway = $GatewayUrl.TrimEnd("/")
$productId = [Guid]"22222222-2222-2222-2222-222222222222"
$customerId = [Guid]"11111111-1111-1111-1111-111111111111"

function Wait-ForFinalOrderStatus {
    param(
        [Parameter(Mandatory)]
        [Guid]$OrderId,

        [Parameter(Mandatory)]
        [ValidateSet("Confirmed", "Cancelled")]
        [string]$ExpectedStatus
    )

    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        $order = Invoke-RestMethod `
            -Method Get `
            -Uri "$gateway/orders/$OrderId"

        if ($order.status -eq $ExpectedStatus) {
            return $order
        }

        if ($order.status -notin @("Pending", $ExpectedStatus)) {
            throw "Order $OrderId reached unexpected status '$($order.status)'."
        }

        Start-Sleep -Milliseconds 500
    } while ([DateTime]::UtcNow -lt $deadline)

    throw "Order $OrderId did not reach '$ExpectedStatus' within $TimeoutSeconds seconds."
}

Write-Host "Checking Gateway health at $gateway ..."
$health = Invoke-RestMethod -Method Get -Uri "$gateway/health/live"
if ($health -ne "Healthy") {
    throw "Gateway health check returned '$health'."
}

Write-Host "Setting product $productId stock to 10 ..."
$stockBody = @{ availableQuantity = 10 } | ConvertTo-Json
Invoke-RestMethod `
    -Method Put `
    -Uri "$gateway/api/inventory/stock/$productId" `
    -ContentType "application/json" `
    -Body $stockBody | Out-Null

Write-Host "Creating an order for 2 units; expected final status: Confirmed ..."
$successfulOrderBody = @{
    customerId = $customerId
    items = @(
        @{
            productId = $productId
            quantity = 2
        }
    )
} | ConvertTo-Json -Depth 5

$successfulOrder = Invoke-RestMethod `
    -Method Post `
    -Uri "$gateway/orders" `
    -ContentType "application/json" `
    -Body $successfulOrderBody
$confirmedOrder = Wait-ForFinalOrderStatus `
    -OrderId $successfulOrder.id `
    -ExpectedStatus "Confirmed"

Write-Host "Creating an order for 1000 units; expected final status: Cancelled ..."
$failedOrderBody = @{
    customerId = $customerId
    items = @(
        @{
            productId = $productId
            quantity = 1000
        }
    )
} | ConvertTo-Json -Depth 5

$failedOrder = Invoke-RestMethod `
    -Method Post `
    -Uri "$gateway/orders" `
    -ContentType "application/json" `
    -Body $failedOrderBody
$cancelledOrder = Wait-ForFinalOrderStatus `
    -OrderId $failedOrder.id `
    -ExpectedStatus "Cancelled"

$finalStock = Invoke-RestMethod `
    -Method Get `
    -Uri "$gateway/api/inventory/stock/$productId"

if ([decimal]$finalStock.availableQuantity -ne 8) {
    throw "Expected 8 units after the workflow, but found $($finalStock.availableQuantity)."
}

[PSCustomObject]@{
    Gateway = $gateway
    ProductId = $productId
    ConfirmedOrderId = $confirmedOrder.id
    ConfirmedOrderStatus = $confirmedOrder.status
    CancelledOrderId = $cancelledOrder.id
    CancelledOrderStatus = $cancelledOrder.status
    FinalAvailableQuantity = $finalStock.availableQuantity
}
