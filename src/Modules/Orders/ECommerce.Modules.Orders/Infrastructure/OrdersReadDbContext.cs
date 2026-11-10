using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace ECommerce.Modules.Orders.Infrastructure;

internal sealed class OrdersReadDbContext : OrdersDbContext
{
    public OrdersReadDbContext(DbContextOptions<OrdersReadDbContext> options) : base(options)
    {
        ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
        ChangeTracker.AutoDetectChangesEnabled = false;
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
        => throw new InvalidOperationException("OrdersReadDbContext is read-only.");

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("OrdersReadDbContext is read-only.");
}