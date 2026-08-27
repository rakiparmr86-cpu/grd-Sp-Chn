using Microsoft.AspNetCore.Mvc;

namespace GRD.SpChn.Identity.Api.Controllers;

[ApiController]
[Route("health")]
public class IdentityController : ControllerBase
{
    [HttpGet]
    public IActionResult Health()
    {
        return Ok(new
        {
            service = "Identity",
            status = "Running"
        });
    }
}
