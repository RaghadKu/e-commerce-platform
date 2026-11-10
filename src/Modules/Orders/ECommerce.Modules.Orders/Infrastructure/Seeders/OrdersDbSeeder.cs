
using ECommerce.Modules.Orders.Domain;
using ECommerce.Modules.Orders.Infrastructure.Seeders;
using ECommerce.Modules.Products.Infrastructure.Seeders;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace ECommerce.Modules.Orders.Infrastructure.Seeders;

internal sealed class OrdersDbSeeder(
    OrdersDbContext db) : IOrdersDbSeeder
{
    private const int OrderCount = 10_000;
    private const int BatchSize = 500;
    private const string SeedPrefix = "ECommerce.DevOrders.V1";

    public async Task<int> SeedAsync(
        IReadOnlyList<Guid> customerIds,
        IReadOnlyList<ProductSeedData> products,
        CancellationToken cancellationToken)
    {
        var customers = customerIds
            .Where(id => id != Guid.Empty)
            .Distinct()
            .OrderBy(id => id)
            .ToArray();

        var availableProducts = products
            .Where(p => p.ProductId != Guid.Empty && p.Price >= 0)
            .GroupBy(p => p.ProductId)
            .Select(g => g.First())
            .OrderBy(p => p.ProductId)
            .ToArray();

        if (customers.Length == 0)
            throw new InvalidOperationException(
                "Customers must be seeded before orders.");

        if (availableProducts.Length == 0)
            throw new InvalidOperationException(
                "Products must be seeded before orders.");

        var random = new Random(20261110);
        var createdCount = 0;

        for (var start = 1; start <= OrderCount; start += BatchSize)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var end = Math.Min(start + BatchSize, OrderCount);

            var batch = new List<Order>(end - start);

            for (var i = start; i <= end; i++)
            {
                var orderId = CreateDeterministicGuid(
                    $"{SeedPrefix}:Order:{i}");

                var customerId = customers[
                    random.Next(customers.Length)];

                var createdAt = DateTime.UtcNow.AddDays(
                    -random.Next(0, 366))
                    .AddMinutes(-random.Next(0, 1440));

                var order = new Order
                {
                    Id = orderId,
                    UserId = customerId,
                    Status = GenerateStatus(random),
                    CreatedAtUtc = createdAt,
                    UpdatedAtUtc = createdAt,
                    Items = GenerateItems(
                        orderId,
                        availableProducts,
                        random)
                };

                batch.Add(order);
            }

            var orderIds = batch
                .Select(o => o.Id)
                .ToArray();

            var existingIds = await db.Orders
                .AsNoTracking()
                .Where(o => orderIds.Contains(o.Id))
                .Select(o => o.Id)
                .ToListAsync(cancellationToken);

            var existingSet = existingIds.ToHashSet();

            var newOrders = batch
                .Where(o => !existingSet.Contains(o.Id))
                .ToList();

            if (newOrders.Count == 0)
                continue;

            db.Orders.AddRange(newOrders);

            await db.SaveChangesAsync(cancellationToken);

            createdCount += newOrders.Count;

            db.ChangeTracker.Clear();
        }

        return createdCount;
    }

    private static List<OrderItem> GenerateItems(
        Guid orderId,
        ProductSeedData[] products,
        Random random)
    {
        var itemCount = random.Next(1, 6);

        var selectedProducts = new HashSet<int>();

        var items = new List<OrderItem>(itemCount);

        while (items.Count < itemCount)
        {
            var productIndex = random.Next(products.Length);

            if (!selectedProducts.Add(productIndex))
                continue;

            var product = products[productIndex];

            items.Add(new OrderItem
            {
                Id = CreateDeterministicGuid(
                    $"{SeedPrefix}:OrderItem:{orderId}:{product.ProductId}"),

                OrderId = orderId,
                ProductId = product.ProductId,
                Qty = random.Next(1, 5),
                Price = product.Price
            });
        }

        return items;
    }

    private static OrderStatus GenerateStatus(Random random)
    {
        var value = random.Next(100);

        return value switch
        {
            < 5 => OrderStatus.Draft,
            < 15 => OrderStatus.Pending,
            < 30 => OrderStatus.Confirmed,
            < 45 => OrderStatus.Processing,
            < 60 => OrderStatus.Shipped,
            < 90 => OrderStatus.Delivered,
            _ => OrderStatus.Cancelled
        };
    }

    private static Guid CreateDeterministicGuid(string value)
    {
        var hash = SHA256.HashData(
            Encoding.UTF8.GetBytes(value));

        return new Guid(hash.AsSpan(0, 16));
    }
}
