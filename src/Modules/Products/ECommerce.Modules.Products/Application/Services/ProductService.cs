using ECommerce.Modules.Products.Application.Mapping;
using ECommerce.Modules.Products.Domain;
using ECommerce.Modules.Products.Infrastructure;
using ECommerce.SharedKernel.Exceptions;
using ECommerce.SharedKernel.Pagination;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Modules.Products.Application.Services;

internal class ProductService(ProductsDbContext db) : IProductService
{
    public async Task<PagedResult<ProductDto>> GetAllAsync(ProductQuery query, CancellationToken ct)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var products = db.Products.AsNoTracking();
        if (query.CategoryId is { } categoryId)
            products = products.Where(p => p.CategoryId == categoryId);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            products = products.Where(p => p.Name.Contains(search));
        }

        var total = await products.CountAsync(ct);
        var items = await products
            .Include(p => p.Category).Include(p => p.Images)
            .OrderBy(p => p.Name).ThenBy(p => p.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<ProductDto>(items.Select(p => p.ToDto()).ToList(), page, pageSize, total);
    }

    public async Task<ProductDto> GetByIdAsync(Guid id, CancellationToken ct)
    {
        return (await db.Products.AsNoTracking()
            .Include(p => p.Category).Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw NotFound()).ToDto();
    }
    public async Task<ProductDto> CreateAsync(ProductRequest request, CancellationToken ct)
    {
        var category = await db.Categories.FirstOrDefaultAsync(c => c.Id == request.CategoryId, ct)
               ?? throw CategoryMissing();

        var sku = NormalizeSku(request.Sku);
        if (await db.Products.AnyAsync(p => p.Sku == sku, ct))
            throw new ConflictException("A product with this SKU already exists.");

        var now = DateTime.UtcNow;
        var product = new Product
        {
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            Price = request.Price,
            Sku = sku,
            Category = category,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        db.Products.Add(product);
        await db.SaveOrConflictAsync(ct);

        return product.ToDto();
    }

    public async Task<ProductDto> UpdateAsync(Guid id, ProductRequest request, CancellationToken ct)
    {
        var product = await db.Products.Include(p => p.Category).Include(p => p.Images)
                  .FirstOrDefaultAsync(p => p.Id == id, ct)
              ?? throw NotFound();

        if (product.CategoryId != request.CategoryId)
        {
            product.Category = await db.Categories.FirstOrDefaultAsync(c => c.Id == request.CategoryId, ct)
                               ?? throw CategoryMissing();
        }

        var sku = NormalizeSku(request.Sku);
        if (await db.Products.AnyAsync(p => p.Sku == sku && p.Id != id, ct))
            throw new ConflictException("A product with this SKU already exists.");

        product.Name = request.Name.Trim();
        product.Description = request.Description?.Trim();
        product.Price = request.Price;
        product.Sku = sku;
        product.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveOrConflictAsync(ct);
        return product.ToDto();
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == id, ct)
              ?? throw NotFound();

        product.IsDeleted = true;
        product.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveOrConflictAsync(ct);
    }

    private static string NormalizeSku(string sku) => sku.Trim().ToUpperInvariant();

    private static NotFoundException NotFound() => new("Product not found.");

    private static ValidationFailedException CategoryMissing() =>
        new(new Dictionary<string, string[]> { ["categoryId"] = ["The category does not exist."] });
}
