using Microsoft.AspNetCore.Mvc;

namespace GRD.SpChn.Identity.Api.Controllers;

[ApiController]
[Route("health/details")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new
        {
            service = "Identity",
            status = "Healthy",
            time = DateTime.UtcNow
        });
    }
}
