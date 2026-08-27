using GRD.SpChn.Security;
using GRD.SpChn.SharedKernel;
using GRD.SpChn.Warehouse.Application.Receiving;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GRD.SpChn.Warehouse.Api.Controllers;

[ApiController]
[Route("purchase-orders")]
public sealed class GoodsReceiptsController(ISender sender) : ControllerBase
{
    [Authorize(Policy = ErpPolicies.GoodsReceiptRead)]
    [HttpGet("{purchaseOrderId:guid}")]
    public async Task<IActionResult> GetExpectedPurchaseOrder(
        Guid purchaseOrderId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetExpectedPurchaseOrderQuery(purchaseOrderId),
            cancellationToken);
        if (result.IsFailure) return ToProblem(result);
        if (result.Value.DestinationOrganizationUnitId != User.GetRequiredOrganizationUnitId())
            return Forbid();
        return Ok(result.Value);
    }

    [Authorize(Policy = ErpPolicies.GoodsReceiptPost)]
    [HttpPost("{purchaseOrderId:guid}/goods-receipts")]
    public async Task<IActionResult> PostGoodsReceipt(
        Guid purchaseOrderId,
        [FromBody] PostGoodsReceiptRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new PostGoodsReceiptCommand(
            purchaseOrderId,
            User.GetRequiredOrganizationUnitId(),
            User.GetRequiredUserId(),
            (request.Items ?? []).Select(item => new PostGoodsReceiptItem(
                item.ProductId,
                item.Quantity,
                item.UnitOfMeasure)).ToArray()), cancellationToken);
        return result.IsSuccess
            ? Created($"/goods-receipts/{result.Value.Id}", result.Value)
            : ToProblem(result);
    }

    private IActionResult ToProblem<T>(Result<T> result) => Problem(
        statusCode: result.FirstError.Type switch
        {
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            _ when result.FirstError.Code == "Warehouse.WrongReceivingLocation" =>
                StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status400BadRequest
        },
        title: result.FirstError.Code,
        detail: result.FirstError.Description);
}

public sealed record PostGoodsReceiptRequest(
    IReadOnlyCollection<PostGoodsReceiptItemRequest>? Items);
public sealed record PostGoodsReceiptItemRequest(
    Guid ProductId,
    decimal Quantity,
    string UnitOfMeasure);
