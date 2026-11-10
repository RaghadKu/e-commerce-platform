using ECommerce.SharedKernel.Pagination;

namespace ECommerce.Modules.Orders.Application.Services;

public interface IOrderService
{
    Task<OrderDto> AddItemAsync(AddOrderItemRequest request, CancellationToken ct);
    Task<OrderDto> RemoveItemAsync(Guid orderItemId, CancellationToken ct);
    Task<OrderDto> UpdateItemAsync(UpdateOrderItemRequest request, CancellationToken ct);
    Task<PagedResult<OrderDto>> GetAllAsync(OrderQuery input, CancellationToken cancellationToken);
    Task<OrderDto> GetByIdAsync(Guid Id, CancellationToken ct);
    Task<OrderDto> CheckoutAsync(CancellationToken cancellationToken);
    Task<OrderDto> ConfirmAsync(CancellationToken cancellationToken);
    Task<OrderDto> CancelAsync(CancellationToken cancellationToken);
}
