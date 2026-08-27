using Microsoft.AspNetCore.Mvc;

namespace GRD.SpChn.Notifications.Api.Controllers;

[ApiController]
[Route("health")]
public class NotificationsController : ControllerBase
{
    [HttpGet]
    public IActionResult Health()
    {
        return Ok(new
        {
            service = "Notifications",
            status = "Running"
        });
    }
}
