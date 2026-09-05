using GRD.SpChn.ProductCatalog.Application.Items;
using GRD.SpChn.Security;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GRD.SpChn.ProductCatalog.Api.Controllers;

[ApiController]
[Route("items")]
public sealed class ItemsController(ISender sender) : ControllerBase
{
    [Authorize(Policy = ErpPolicies.CatalogItemRead)]
    [HttpGet]
    public async Task<IActionResult> GetProcurementItems(CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetProcurementItemsQuery(), cancellationToken));
}
