using GRD.SpChn.Security;
using GRD.SpChn.Supplier.Application.Suppliers;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GRD.SpChn.Supplier.Api.Controllers;

[ApiController]
[Route("catalog")]
public sealed class SuppliersController(ISender sender) : ControllerBase
{
    [Authorize(Policy = ErpPolicies.SupplierRead)]
    [HttpGet]
    public async Task<IActionResult> GetActive(CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetActiveSuppliersQuery(), cancellationToken));
}
