using GRD.SpChn.Identity.Application.AccessProfiles.GetAccessProfiles;
using GRD.SpChn.Identity.Application.AccessProfiles.GetPermissionCatalog;
using GRD.SpChn.Identity.Application.AccessProfiles.ReplacePermissions;
using GRD.SpChn.Security;
using GRD.SpChn.SharedKernel;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GRD.SpChn.Identity.Api.Controllers;

[ApiController]
[Route("access-profiles")]
[Authorize(Policy = ErpPolicies.IdentityAccessProfileManage)]
public sealed class AccessProfilesController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetAccessProfilesQuery(), cancellationToken));

    [HttpGet("permissions")]
    public async Task<IActionResult> GetPermissionCatalog(CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetPermissionCatalogQuery(), cancellationToken));

    [HttpPut("{code}/permissions")]
    [ProducesResponseType<AccessProfileDetailsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReplacePermissions(
        string code,
        [FromBody] ReplaceAccessProfilePermissionsRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new ReplaceAccessProfilePermissionsCommand(code, request.PermissionCodes),
            cancellationToken);
        if (result.IsSuccess)
        {
            return Ok(result.Value);
        }

        var error = result.FirstError;
        return Problem(
            statusCode: error.Type == ErrorType.NotFound
                ? StatusCodes.Status404NotFound
                : StatusCodes.Status400BadRequest,
            title: error.Code,
            detail: error.Description);
    }
}

public sealed record ReplaceAccessProfilePermissionsRequest(
    IReadOnlyCollection<string> PermissionCodes);
