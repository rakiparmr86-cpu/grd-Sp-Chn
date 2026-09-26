using GRD.SpChn.Procurement.Application.PurchaseOrders;
using GRD.SpChn.Security;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GRD.SpChn.Procurement.Api.Controllers;

[ApiController]
[Route("purchase-orders")]
public sealed class PurchaseOrdersController(ISender sender) : ControllerBase
{
    [Authorize(Policy = ErpPolicies.PurchaseOrderRead)]
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        CancellationToken cancellationToken)
    {
        if (fromUtc is not null && toUtc is not null && toUtc <= fromUtc)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Procurement.InvalidDateRange",
                detail: "The To date must be after the From date.");
        }

        var includeAllOrganizationUnits = User.HasClaim(
            ErpClaimTypes.Permission,
            ErpPermissions.PurchaseOrderCreate);
        var orders = await sender.Send(new ListPurchaseOrdersQuery(
            User.GetRequiredOrganizationUnitId(),
            includeAllOrganizationUnits,
            fromUtc?.ToUniversalTime(),
            toUtc?.ToUniversalTime()), cancellationToken);
        return Ok(orders);
    }

    [Authorize(Policy = ErpPolicies.PurchaseOrderRead)]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetPurchaseOrderQuery(id), cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value)
            : Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: result.FirstError.Code,
                detail: result.FirstError.Description);
    }

    [Authorize(Policy = ErpPolicies.PurchaseOrderDispatch)]
    [HttpPost("{id:guid}/dispatch")]
    public async Task<IActionResult> MarkDispatched(
        Guid id,
        [FromBody] RecordVendorDispatchRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new MarkPurchaseOrderDispatchedCommand(
            id,
            User.GetRequiredUserId(),
            request.VendorDispatchReference,
            request.DeliveryChallanNumber,
            request.TransporterName,
            request.VehicleNumber,
            request.DispatchedOnUtc ?? DateTime.UtcNow,
            request.ExpectedDeliveryOnUtc,
            request.Notes), cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value)
            : Problem(
                statusCode: result.FirstError.Type == GRD.SpChn.SharedKernel.ErrorType.NotFound
                    ? StatusCodes.Status404NotFound
                    : StatusCodes.Status409Conflict,
                title: result.FirstError.Code,
                detail: result.FirstError.Description);
    }
}

public sealed record RecordVendorDispatchRequest(
    string VendorDispatchReference,
    string? DeliveryChallanNumber,
    string? TransporterName,
    string? VehicleNumber,
    DateTime? DispatchedOnUtc,
    DateTime? ExpectedDeliveryOnUtc,
    string? Notes);
