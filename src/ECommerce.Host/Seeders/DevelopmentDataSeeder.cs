using ECommerce.Inventory.Contracts;
using ECommerce.Modules.Products.Infrastructure.Seeders;
using ECommerce.Modules.Users.Infrastructure;

namespace ECommerce.Host.Seeders;

internal sealed class DevelopmentDataSeeder(
    IUsersDbSeeder usersSeeder,
    IProductsDbSeeder productsSeeder,
    ICategoriesDbSeeder categoriesSeeder,
    InventoryService.InventoryServiceClient inventoryClient)
{
    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        await usersSeeder.SeedAsync(cancellationToken);

        var categoryIds = await categoriesSeeder.SeedAsync(cancellationToken);

        var productIds = await productsSeeder.SeedAsync(categoryIds, cancellationToken);

        var request = new SeedStockRequest();

        request.ProductIds.AddRange(
            productIds.Select(id => id.ToString()));

        var response = await inventoryClient.SeedStockAsync(
            request,
            cancellationToken: cancellationToken);
    }
}
