using ECommerce.Inventory.Contracts;
using ECommerce.Modules.Orders.Infrastructure.Seeders;
using ECommerce.Modules.Products.Infrastructure.Seeders;
using ECommerce.Modules.Users.Infrastructure;

namespace ECommerce.Host.Seeders;

internal sealed class DevelopmentDataSeeder(
    IUsersDbSeeder customerSeeder,
    IProductsDbSeeder productsSeeder,
    ICategoriesDbSeeder categoriesSeeder,
    IOrdersDbSeeder ordersSeeder,
    InventoryService.InventoryServiceClient inventoryClient)
{
    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        var customerIds = await customerSeeder.SeedAsync(
            cancellationToken);

        var categoryIds = await categoriesSeeder.SeedAsync(
            cancellationToken);

        var productData = await productsSeeder.SeedAsync(
            categoryIds,
            cancellationToken);

        var request = new SeedStockRequest();

        request.ProductIds.AddRange(
            productData.Select(p => p.ProductId.ToString()));

        await inventoryClient.SeedStockAsync(
            request,
            cancellationToken: cancellationToken);

        var orderProducts = productData
            .Select(p => new ProductSeedData(
                p.ProductId,
                p.Price))
            .ToList();

        var createdOrders = await ordersSeeder.SeedAsync(
            customerIds,
            orderProducts,
            cancellationToken);

        Console.WriteLine(
            $"Development seeding completed. Created {createdOrders} orders.");
    }
}
