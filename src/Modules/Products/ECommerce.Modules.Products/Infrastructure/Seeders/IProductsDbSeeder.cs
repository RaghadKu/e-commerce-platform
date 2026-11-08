namespace ECommerce.Modules.Products.Infrastructure.Seeders;

public interface IProductsDbSeeder
{
    Task<IReadOnlyList<Guid>> SeedAsync(
        IReadOnlyList<Guid> categoryIds,
        CancellationToken cancellationToken);
}
