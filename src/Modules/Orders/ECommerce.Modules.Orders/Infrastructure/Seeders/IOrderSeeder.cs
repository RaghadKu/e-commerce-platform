using ECommerce.Modules.Products.Infrastructure.Seeders;

namespace ECommerce.Modules.Orders.Infrastructure.Seeders;

public interface IOrdersDbSeeder
{
    Task<int> SeedAsync(
        IReadOnlyList<Guid> customerIds,
        IReadOnlyList<ProductSeedData> products,
        CancellationToken cancellationToken);
}