using Microsoft.AspNetCore.Mvc;

namespace ECommerce.Modules.Users.Api;

[ApiController]
[Route("api/users/ping")]
public sealed class UsersPingController : ControllerBase
{
    [HttpGet]
    public IActionResult Ping() => Ok(new { module = "Users", status = "up" });
}
