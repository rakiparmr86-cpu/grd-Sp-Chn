using Microsoft.AspNetCore.Mvc;

namespace GRD.SpChn.Notifications.Api.Controllers;

[ApiController]
[Route("api/health")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new
        {
            service = "Notifications",
            status = "Healthy",
            time = DateTime.UtcNow
        });
    }
}