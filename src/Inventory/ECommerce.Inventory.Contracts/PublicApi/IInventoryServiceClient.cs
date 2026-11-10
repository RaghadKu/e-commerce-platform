namespace ECommerce.Inventory.Contracts.PublicApi;

public interface IInventoryServiceClient
{
    Task<int> GetAvailableAsync(Guid productId, CancellationToken ct);
    Task<StockLevelDto> SetStockAsync(Guid productId, int quantityOnHand, CancellationToken ct);
    Task ReserveStockAsync(Guid orderId, List<(Guid ProductId, int Quantity)> lines, CancellationToken ct);
    Task ReleaseStockAsync(Guid orderId, CancellationToken ct);
}
