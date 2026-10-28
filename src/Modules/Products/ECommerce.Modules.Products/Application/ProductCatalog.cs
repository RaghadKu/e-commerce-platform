using ECommerce.Modules.Products.Infrastructure;
using ECommerce.Modules.Products.PublicApi;
using Microsoft.EntityFrameworkCore;
using static ECommerce.Modules.Products.PublicApi.IProductCatalog;

namespace ECommerce.Modules.Products.Application;

internal class ProductCatalog(ProductsDbContext db) : IProductCatalog
{
    public async Task<IReadOnlyList<ProductSnapshot>> GetProductsAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default)
    {
        return await db.Products.AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .Select(p => new ProductSnapshot(p.Id, p.Name, p.Price))
            .ToListAsync(cancellationToken);
    }
}
