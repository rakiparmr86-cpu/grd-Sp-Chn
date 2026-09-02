using GRD.SpChn.Procurement.Application.MaterialRequests;
using GRD.SpChn.Procurement.Application.PurchaseOrders;
using GRD.SpChn.Security;
using GRD.SpChn.SharedKernel;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GRD.SpChn.Procurement.Api.Controllers;

[ApiController]
[Route("material-requests")]
public sealed class MaterialRequestsController(ISender sender) : ControllerBase
{
    [Authorize(Policy = ErpPolicies.MaterialRequestRead)]
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var includeAllOrganizationUnits = User.HasClaim(
            ErpClaimTypes.Permission,
            ErpPermissions.MaterialRequestApprove);
        var requests = await sender.Send(new ListMaterialRequestsQuery(
            User.GetRequiredOrganizationUnitId(),
            includeAllOrganizationUnits), cancellationToken);
        return Ok(requests);
    }

    [Authorize(Policy = ErpPolicies.MaterialRequestCreate)]
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateMaterialRequestRequest request,
        CancellationToken cancellationToken)
    {
        var organizationUnitId = User.GetRequiredOrganizationUnitId();
        var result = await sender.Send(new CreateMaterialRequestCommand(
            organizationUnitId,
            organizationUnitId,
            User.GetRequiredUserId(),
            request.Purpose,
            (request.Items ?? []).Select(item => new CreateMaterialRequestItem(
                item.ProductId,
                item.Quantity,
                item.UnitOfMeasure)).ToArray()), cancellationToken);
        return result.IsSuccess
            ? AcceptedAtAction(nameof(GetById), new { id = result.Value.Id }, result.Value)
            : ToProblem(result);
    }

    [Authorize(Policy = ErpPolicies.MaterialRequestRead)]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetMaterialRequestQuery(id), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ToProblem(result);
    }

    [Authorize(Policy = ErpPolicies.MaterialRequestApprove)]
    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new ApproveMaterialRequestCommand(id, User.GetRequiredUserId()),
            cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ToProblem(result);
    }

    [Authorize(Policy = ErpPolicies.PurchaseOrderCreate)]
    [HttpPost("{id:guid}/purchase-orders")]
    public async Task<IActionResult> IssuePurchaseOrder(
        Guid id,
        [FromBody] IssuePurchaseOrderRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new IssuePurchaseOrderCommand(
            id,
            request.SupplierId,
            request.Currency,
            (request.Prices ?? []).Select(price =>
                new PurchaseOrderPrice(price.ProductId, price.UnitPrice)).ToArray()),
            cancellationToken);
        return result.IsSuccess
            ? Created($"/purchase-orders/{result.Value.Id}", result.Value)
            : ToProblem(result);
    }

    private IActionResult ToProblem<T>(Result<T> result) => Problem(
        statusCode: result.FirstError.Type switch
        {
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest
        },
        title: result.FirstError.Code,
        detail: result.FirstError.Description);
}

public sealed record CreateMaterialRequestRequest(
    string Purpose,
    IReadOnlyCollection<CreateMaterialRequestItemRequest>? Items);
public sealed record CreateMaterialRequestItemRequest(
    Guid ProductId,
    decimal Quantity,
    string UnitOfMeasure);
public sealed record IssuePurchaseOrderRequest(
    Guid SupplierId,
    string Currency,
    IReadOnlyCollection<PurchaseOrderPriceRequest>? Prices);
public sealed record PurchaseOrderPriceRequest(Guid ProductId, decimal UnitPrice);
