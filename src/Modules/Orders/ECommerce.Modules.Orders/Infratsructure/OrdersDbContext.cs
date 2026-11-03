using ECommerce.Modules.Orders.Domain;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Modules.Orders.Infrastructure;

internal sealed class OrdersDbContext(DbContextOptions<OrdersDbContext> options) : DbContext(options)
{
    public const string Schema = "orders";

    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.HasDefaultSchema(Schema);

        builder.Entity<Order>(b =>
        {
            b.ToTable("Orders");
            b.HasKey(o => o.Id);

            b.Property(o => o.UserId)
                .IsRequired();

            b.Property(o => o.Status)
                .HasConversion<string>()
                .HasMaxLength(50)
                .IsRequired();

            b.Property(o => o.CreatedAtUtc)
                .HasDefaultValueSql("GETDATE()")
                .IsRequired();

            b.Property(o => o.UpdatedAtUtc)
                .HasDefaultValueSql("GETDATE()")
                .IsRequired();

            b.HasMany(e => e.Items)
                .WithOne(e => e.Order)
                .HasForeignKey(e => e.OrderId);
        });

        builder.Entity<OrderItem>(b =>
        {
            b.ToTable("OrderItems");

            b.HasKey(i => i.Id);

            b.Property(i => i.Price)
                .HasPrecision(18, 2);

            b.Property(i => i.Qty)
                .IsRequired();
        });
    }
}