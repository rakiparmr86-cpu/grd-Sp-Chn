using GRD.SpChn.Identity.Application.Users.CreateUser;
using GRD.SpChn.Identity.Application.Users.GetAssignableAccessProfiles;
using GRD.SpChn.Security;
using GRD.SpChn.SharedKernel;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GRD.SpChn.Identity.Api.Controllers;

[ApiController]
[Route("users")]
[Authorize(Policy = ErpPolicies.IdentityUserCreate)]
public sealed class UsersController(ISender sender) : ControllerBase
{
    [HttpGet("access-profiles")]
    public async Task<IActionResult> GetAccessProfiles(
        CancellationToken cancellationToken) =>
        Ok(await sender.Send(
            new GetAssignableAccessProfilesQuery(),
            cancellationToken));

    [HttpPost]
    [ProducesResponseType<CreateUserResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] CreateUserRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new CreateUserCommand(
            request.UserName,
            request.Password,
            request.AccessProfile,
            request.OrganizationUnitId), cancellationToken);

        if (result.IsSuccess)
        {
            return StatusCode(StatusCodes.Status201Created, result.Value);
        }

        var error = result.FirstError;
        return Problem(
            statusCode: error.Type == ErrorType.Conflict
                ? StatusCodes.Status409Conflict
                : StatusCodes.Status400BadRequest,
            title: error.Code,
            detail: error.Description);
    }
}

public sealed record CreateUserRequest(
    string UserName,
    string Password,
    string AccessProfile,
    Guid OrganizationUnitId);
