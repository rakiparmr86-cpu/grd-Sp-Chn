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
}
