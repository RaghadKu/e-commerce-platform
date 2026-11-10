using Microsoft.EntityFrameworkCore;

namespace ECommerce.Modules.Products.Infrastructure;

internal sealed class ProductsReadDbContext : ProductsDbContext
{
    public ProductsReadDbContext(DbContextOptions<ProductsReadDbContext> options) : base(options)
    {
        ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
        ChangeTracker.AutoDetectChangesEnabled = false;
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
        => throw new InvalidOperationException("ProductsReadDbContext is read-only.");

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("ProductsReadDbContext is read-only.");
}
