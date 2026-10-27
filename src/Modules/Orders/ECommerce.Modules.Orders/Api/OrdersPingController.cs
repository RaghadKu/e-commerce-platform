using Microsoft.AspNetCore.Mvc;

namespace ECommerce.Modules.Orders.Api;

[ApiController]
[Route("api/orders/ping")]
public sealed class OrdersPingController : ControllerBase
{
    [HttpGet]
    public IActionResult Ping() => Ok(new { module = "Orders", status = "up" });
}
