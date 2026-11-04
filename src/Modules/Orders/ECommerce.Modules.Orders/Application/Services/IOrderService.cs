namespace ECommerce.Modules.Orders.Application.Services;

public interface IOrderService
{
    public Task<OrderDto> AddItemAsync(AddOrderItemRequest request, CancellationToken ct);
    public Task<OrderDto> RemoveItemAsync(Guid orderItemId, CancellationToken ct);
    public Task<OrderDto> UpdateItemAsync(UpdateOrderItemRequest request, CancellationToken ct);
    public Task<List<OrderDto>> GetAllAsync(CancellationToken cancellationToken);
    public Task<OrderDto> GetByIdAsync(Guid Id, CancellationToken ct);
    public Task<OrderDto> CheckoutAsync(CancellationToken cancellationToken);
    public Task<OrderDto> ConfirmAsync(CancellationToken cancellationToken);
    public Task<OrderDto> CancelAsync(CancellationToken cancellationToken);
}
