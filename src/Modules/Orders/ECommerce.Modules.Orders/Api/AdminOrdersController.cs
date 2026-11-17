using ECommerce.Modules.Orders.Application;
using ECommerce.Modules.Orders.Application.Services;
using ECommerce.SharedKernel.Pagination;
using ECommerce.SharedKernel.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.Modules.Orders.Api;

[ApiController]
[Authorize(Roles = AppRoles.Admin)]
[Route("api/admin/orders")]
public class AdminOrdersController(IAdminOrderService orders) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResult<OrderDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] OrderQuery query, CancellationToken ct)
        => Ok(await orders.GetAllAsync(query, ct));

    [HttpPut("{id:guid}/status")]
    [ProducesResponseType<OrderDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> UpdateStatus(Guid id, UpdateOrderStatusRequest request, CancellationToken ct)
        => Ok(await orders.ChangeStatusAsync(id, request, ct));
}