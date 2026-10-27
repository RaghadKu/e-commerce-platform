using Microsoft.AspNetCore.Mvc;

namespace ECommerce.Modules.Notifications.Api;

[ApiController]
[Route("api/notifications/ping")]
public sealed class NotificationsPingController : ControllerBase
{
    [HttpGet]
    public IActionResult Ping() => Ok(new { module = "Notifications", status = "up" });
}
