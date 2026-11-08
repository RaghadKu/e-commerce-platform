namespace ECommerce.Modules.Products.Infrastructure.Seeders;

public interface ICategoriesDbSeeder
{
    Task<IReadOnlyList<Guid>> SeedAsync(
        CancellationToken cancellationToken);
}
