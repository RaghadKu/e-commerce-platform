
using ECommerce.Modules.Products.Domain;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Modules.Products.Infrastructure.Seeders;

internal sealed class ProductsDbSeeder(
    ProductsDbContext db) : IProductsDbSeeder
{
    private const int ProductsPerCategory = 20;
    private const int BatchSize = 500;

    private static readonly string[] ProductVariants =
    [
        "Essential", "Classic", "Plus", "Pro", "Premium",
        "Ultra", "Compact", "Max", "Lite", "Advanced",
        "Smart", "Wireless", "Portable", "Performance",
        "Studio", "Everyday", "Professional", "Elite",
        "Signature", "Modern"
    ];

    public async Task<IReadOnlyList<ProductSeedData>> SeedAsync(
        IReadOnlyList<Guid> categoryIds,
        CancellationToken cancellationToken)
    {
        var distinctCategoryIds = categoryIds.Distinct().ToArray();

        if (distinctCategoryIds.Length == 0)
            throw new InvalidOperationException(
                "Categories must be seeded before products.");

        var categories = await db.Categories
            .AsNoTracking()
            .Where(c => distinctCategoryIds.Contains(c.Id))
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);

        if (categories.Count != distinctCategoryIds.Length)
            throw new InvalidOperationException(
                "Some supplied category IDs do not exist.");

        var definitions = categories
            .SelectMany((category, categoryIndex) =>
                ProductVariants.Select((variant, variantIndex) =>
                    new
                    {
                        Sku = $"DEV-{categoryIndex + 1:D2}-{variantIndex + 1:D3}",
                        Name = $"{category.Name} {variant}",
                        Description =
                            $"{variant} {category.Name.ToLowerInvariant()} product for development and testing.",
                        CategoryId = category.Id,
                        Price = Random.Shared.Next(1000, 100000) / 100m
                    }))
            .ToList();

        foreach (var batch in definitions.Chunk(BatchSize))
        {
            var skus = batch.Select(p => p.Sku).ToArray();

            var existingSkus = await db.Products
                .AsNoTracking()
                .Where(p => skus.Contains(p.Sku))
                .Select(p => p.Sku)
                .ToListAsync(cancellationToken);

            var existingSkuSet = existingSkus.ToHashSet();

            var newProducts = batch
                .Where(p => !existingSkuSet.Contains(p.Sku))
                .Select(p => new Product
                {
                    Id = Guid.NewGuid(),
                    Name = p.Name,
                    Description = p.Description,
                    Price = p.Price,
                    Sku = p.Sku,
                    CategoryId = p.CategoryId,
                    IsDeleted = false,
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                })
                .ToList();

            if (newProducts.Count > 0)
            {
                db.Products.AddRange(newProducts);
                await db.SaveChangesAsync(cancellationToken);
                db.ChangeTracker.Clear();
            }
        }

        var allSkus = definitions.Select(p => p.Sku).ToArray();

        var products = await db.Products
            .AsNoTracking()
            .Where(p => allSkus.Contains(p.Sku))
            .Select(p => new ProductSeedData(p.Id, p.Price))
            .ToListAsync(cancellationToken);

        if (products.Count != definitions.Count)
            throw new InvalidOperationException(
                "Some products could not be retrieved after seeding.");

        return products;
    }
}
