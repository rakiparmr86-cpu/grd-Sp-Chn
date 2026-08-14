using Microsoft.AspNetCore.Mvc;

namespace GRD.SpChn.Identity.Api.Controllers;

[ApiController]
[Route("api/notifications")]
public class IdentityController : ControllerBase
{
    [HttpGet("identityhealth")]
    public IActionResult Health()
    {
        return Ok(new
        {
            service = "Notifications",
            status = "Running"
        });
    }
}