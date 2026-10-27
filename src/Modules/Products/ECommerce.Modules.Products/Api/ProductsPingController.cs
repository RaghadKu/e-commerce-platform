using Microsoft.AspNetCore.Mvc;

namespace ECommerce.Modules.Products.Api;

[ApiController]
[Route("api/products/ping")]
public sealed class ProductsPingController : ControllerBase
{
    [HttpGet]
    public IActionResult Ping() => Ok(new { module = "Products", status = "up" });
}
