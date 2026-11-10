using ECommerce.Modules.Orders.Application;
using ECommerce.Modules.Orders.Application.Services;
using ECommerce.SharedKernel.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.Modules.Orders.Api;

[ApiController]
[Authorize]
[Route("api/orders")]
public class OrdersController(IOrderService orderService) : ControllerBase
{
    [HttpPost("items")]
    [ProducesResponseType<OrderDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> AddItem(AddOrderItemRequest request, CancellationToken ct)
    {
        var order = await orderService.AddItemAsync(request, ct);
        return Created($"/api/orders/{order.Id}", order);
    }

    [HttpDelete("items/{id:guid}")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> RemoveItem(Guid id, CancellationToken ct)
    {
        var orderDto = await orderService.RemoveItemAsync(id, ct);
        return Ok(orderDto);
    }

    [HttpPut("items/{id:guid}")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> UpdateItem(UpdateOrderItemRequest request, CancellationToken ct)
    {
        var orderDto = await orderService.UpdateItemAsync(request, ct);
        return Ok(orderDto);
    }

    [HttpGet]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> GetAll([FromQuery] OrderQuery input, CancellationToken ct)
    {
        return Ok(await orderService.GetAllAsync(input, ct));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        return Ok(await orderService.GetByIdAsync(id, ct));
    }

    [HttpPost("checkout")]
    [ProducesResponseType<OrderDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> Checkout(CancellationToken ct)
    {
        var orderDto = await orderService.CheckoutAsync(ct);
        return Ok(orderDto);
    }

    [HttpPost("confirm")]
    [ProducesResponseType<OrderDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> Confirm(CancellationToken ct)
    {
        var orderDto = await orderService.ConfirmAsync(ct);
        return Ok(orderDto);
    }

    [HttpPost("cancel")]
    [ProducesResponseType<OrderDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> Cancel(CancellationToken ct)
    {
        var orderDto = await orderService.CancelAsync(ct);
        return Ok(orderDto);
    }
}
