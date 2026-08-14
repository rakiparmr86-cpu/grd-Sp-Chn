using Microsoft.AspNetCore.Mvc;

namespace GRD.SpChn.Notifications.Api.Controllers;

[ApiController]
[Route("api/notifications")]
public class NotificationsController : ControllerBase
{
    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new
        {
            service = "Notifications",
            status = "Running"
        });
    }
}