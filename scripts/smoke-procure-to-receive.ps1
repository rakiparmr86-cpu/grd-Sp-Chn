[CmdletBinding()]
param(
    [string]$GatewayBaseUrl = "http://localhost:7000",
    [int]$TimeoutSeconds = 30
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Login {
    param([string]$UserName)

    $body = @{
        userName = $UserName
        password = "1223456"
    } | ConvertTo-Json
    return Invoke-RestMethod `
        -Method Post `
        -Uri "$GatewayBaseUrl/api/identity/auth/login" `
        -ContentType "application/json" `
        -Body $body
}

function Invoke-Authorized {
    param(
        [string]$Method,
        [string]$Path,
        [string]$Token,
        [object]$Body
    )

    $parameters = @{
        Method = $Method
        Uri = "$GatewayBaseUrl$Path"
        Headers = @{ Authorization = "Bearer $Token" }
    }
    if ($null -ne $Body) {
        $parameters.ContentType = "application/json"
        $parameters.Body = $Body | ConvertTo-Json -Depth 8
    }
    return Invoke-RestMethod @parameters
}

function Wait-ForResult {
    param([scriptblock]$Operation, [string]$Description)

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    do {
        try {
            $result = & $Operation
            if ($null -ne $result) { return $result }
        }
        catch {
            if ((Get-Date) -ge $deadline) { throw }
        }
        Start-Sleep -Milliseconds 500
    } while ((Get-Date) -lt $deadline)

    throw "Timed out waiting for $Description."
}

$productId = [Guid]::Parse("30000000-0000-0000-0000-000000000004") # STORE-MAIZE
$supplierId = [Guid]::Parse("20000000-0000-0000-0000-000000000001")
$plantId = [Guid]::Parse("00000000-0000-0000-0000-000000000006")

Write-Host "Logging in as the production plant supervisor..." -ForegroundColor Cyan
$supervisor = Login "supervisor.plant@grd.local"
$materialRequest = Invoke-Authorized `
    "Post" `
    "/api/procurement/material-requests" `
    $supervisor.accessToken `
    @{
        purpose = "Raw material for production smoke test"
        items = @(@{ productId = $productId; quantity = 50; unitOfMeasure = "KG" })
    }
Write-Host "Material request created: $($materialRequest.requestNumber)" -ForegroundColor Green

Write-Host "Logging in as Purchase Manager..." -ForegroundColor Cyan
$manager = Login "manager.purchase@grd.local"
$approved = Invoke-Authorized `
    "Post" `
    "/api/procurement/material-requests/$($materialRequest.id)/approve" `
    $manager.accessToken `
    $null
Write-Host "Material request status: $($approved.status)" -ForegroundColor Green

$purchaseOrder = Invoke-Authorized `
    "Post" `
    "/api/procurement/material-requests/$($materialRequest.id)/purchase-orders" `
    $manager.accessToken `
    @{
        supplierId = $supplierId
        currency = "INR"
        prices = @(@{ productId = $productId; unitPrice = 120.50 })
    }
Write-Host "Purchase order issued: $($purchaseOrder.purchaseOrderNumber)" -ForegroundColor Green

Write-Host "Waiting for the PO to reach the production location through RabbitMQ..." -ForegroundColor Cyan
$expectedOrder = Wait-ForResult `
    { Invoke-Authorized "Get" "/api/warehouses/purchase-orders/$($purchaseOrder.id)" $supervisor.accessToken $null } `
    "the Warehouse expected purchase order"
Write-Host "Warehouse PO status: $($expectedOrder.status)" -ForegroundColor Green

$dispatch = Invoke-Authorized `
    "Post" `
    "/api/procurement/purchase-orders/$($purchaseOrder.id)/dispatch" `
    $manager.accessToken `
    @{
        vendorDispatchReference = "SMOKE-DSP-$([DateTime]::UtcNow.ToString('yyyyMMddHHmmss'))"
        deliveryChallanNumber = "SMOKE-CHALLAN"
        transporterName = "GRD Test Transport"
        vehicleNumber = "DL01TEST001"
        dispatchedOnUtc = [DateTime]::UtcNow.ToString("o")
        expectedDeliveryOnUtc = [DateTime]::UtcNow.AddDays(1).ToString("o")
        notes = "Automated procure-to-quality-release smoke test"
    }
Write-Host "Vendor dispatch recorded: $($dispatch.status)" -ForegroundColor Green

$receipt = Invoke-Authorized `
    "Post" `
    "/api/warehouses/purchase-orders/$($purchaseOrder.id)/goods-receipts" `
    $supervisor.accessToken `
    @{
        items = @(@{ productId = $productId; quantity = 50; unitOfMeasure = "KG" })
    }
Write-Host "Goods receipt posted: $($receipt.goodsReceiptNumber)" -ForegroundColor Green

$qualityInspection = Invoke-Authorized `
    "Post" `
    "/api/warehouses/purchase-orders/$($purchaseOrder.id)/quality-inspection" `
    $supervisor.accessToken `
    @{
        result = "Passed"
        notes = "Smoke-test quality checks passed"
    }
Write-Host "Quality inspection: $($qualityInspection.result)" -ForegroundColor Green

$stock = Wait-ForResult `
    { Invoke-Authorized "Get" "/api/inventory/stock/locations/$plantId/$productId" $supervisor.accessToken $null } `
    "location inventory"
$finalOrder = Wait-ForResult `
    {
        $order = Invoke-Authorized "Get" "/api/procurement/purchase-orders/$($purchaseOrder.id)" $manager.accessToken $null
        if ($order.status -eq "Received") { $order } else { $null }
    } `
    "the Procurement Received status"

Write-Host "Procurement status: $($finalOrder.status)" -ForegroundColor Green
Write-Host "Plant on-hand stock: $($stock.onHandQuantity) KG" -ForegroundColor Green
Write-Host "Procure-to-quality-release smoke test completed successfully." -ForegroundColor Green
