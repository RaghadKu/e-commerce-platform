namespace ECommerce.Modules.Products.Infrastructure.Seeders;

public interface IProductsDbSeeder
{
    Task<IReadOnlyList<ProductSeedData>> SeedAsync(
        IReadOnlyList<Guid> categoryIds,
        CancellationToken cancellationToken);
}

public sealed record ProductSeedData(Guid ProductId, decimal Price);
