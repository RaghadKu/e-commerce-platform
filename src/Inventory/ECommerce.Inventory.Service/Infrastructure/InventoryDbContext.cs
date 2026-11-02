using ECommerce.Inventory.Service.Domain;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Inventory.Service.Infrastructure;

internal sealed class InventoryDbContext(DbContextOptions<InventoryDbContext> options) : DbContext(options)
{
    public DbSet<StockItem> StockItems => Set<StockItem>();
    public DbSet<StockReservation> Reservations => Set<StockReservation>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<StockItem>(e =>
        {
            e.ToTable("StockItems", t => t.HasCheckConstraint(
                "CK_StockItems_Quantities",
                "[QuantityReserved] >= 0 AND [QuantityReserved] <= [QuantityOnHand]"));
            e.HasKey(s => s.ProductId);
            e.Property(s => s.ProductId).ValueGeneratedNever();
        });

        builder.Entity<StockReservation>(e =>
        {
            e.ToTable("StockReservations");
            e.HasKey(r => r.Id);
            e.HasIndex(r => r.OrderId).IsUnique();
            e.HasMany(r => r.Lines).WithOne()
                .HasForeignKey(l => l.ReservationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<StockReservationLine>(e =>
        {
            e.ToTable("StockReservationLines");
            e.HasKey(l => new { l.ReservationId, l.ProductId });
        });
    }
}
