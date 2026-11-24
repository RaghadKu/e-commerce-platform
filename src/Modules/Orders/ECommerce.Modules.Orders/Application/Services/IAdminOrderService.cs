using ECommerce.SharedKernel.Pagination;

namespace ECommerce.Modules.Orders.Application.Services;

public record UpdateOrderStatusRequest(string Status);

public interface IAdminOrderService
{
    Task<PagedResult<OrderDto>> GetAllAsync(OrderQuery query, CancellationToken ct);
    Task<OrderDto> ChangeStatusAsync(Guid orderId, UpdateOrderStatusRequest request, CancellationToken ct);
    Task<OrderDto> GetByIdAsync(Guid orderId, CancellationToken ct);
}