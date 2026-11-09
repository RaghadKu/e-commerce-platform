using ECommerce.Modules.Products.Application;

namespace ECommerce.Modules.Products.Infrastructure;

internal interface IInventoryClient
{
    Task<StockLevelDto> GetAvailableAsync(Guid productId, CancellationToken ct);
    Task<StockLevelDto> SetStockAsync(Guid productId, int quantityOnHand, CancellationToken ct);
}