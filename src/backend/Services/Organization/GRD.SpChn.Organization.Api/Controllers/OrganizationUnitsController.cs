using GRD.SpChn.Organization.Application.OrganizationUnits;
using GRD.SpChn.Security;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GRD.SpChn.Organization.Api.Controllers;

[ApiController]
[Route("units")]
public sealed class OrganizationUnitsController(ISender sender) : ControllerBase
{
    [Authorize(Policy = ErpPolicies.OrganizationRead)]
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetOrganizationUnitsQuery(), cancellationToken));

    [Authorize(Policy = ErpPolicies.OrganizationManage)]
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateOrganizationUnitRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new CreateOrganizationUnitCommand(
            request.ParentId,
            request.Code,
            request.Name,
            request.Type), cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetAll), result.Value)
            : Problem(
                statusCode: result.FirstError.Type == GRD.SpChn.SharedKernel.ErrorType.Conflict ? 409 : 400,
                title: result.FirstError.Code,
                detail: result.FirstError.Description);
    }
}

public sealed record CreateOrganizationUnitRequest(
    Guid? ParentId,
    string Code,
    string Name,
    string Type);
