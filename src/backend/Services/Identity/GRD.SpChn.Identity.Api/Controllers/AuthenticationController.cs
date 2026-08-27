using GRD.SpChn.Identity.Application.Authentication.Login;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GRD.SpChn.Identity.Api.Controllers;

[ApiController]
[Route("auth")]
public sealed class AuthenticationController(ISender sender) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request,CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new LoginCommand(request.UserName, request.Password),
            cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : Unauthorized(new
        {
            code = result.FirstError.Code,
            message = result.FirstError.Description
        });
    }
}

public sealed record LoginRequest(string UserName, string Password);
