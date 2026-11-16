using ECommerce.Modules.Orders.Domain;
using ECommerce.Modules.Orders.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Modules.Orders.Infrastructure;

internal class OrdersDbContext : DbContext
{
    public const string Schema = "orders";

    public OrdersDbContext(DbContextOptions<OrdersDbContext> options) : base(options) { }

    protected OrdersDbContext(DbContextOptions options) : base(options) { }

    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

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

        builder.Entity<OutboxMessage>(b =>
        {
            b.ToTable("OutboxMessages");
            b.HasKey(m => m.Id);
            b.Property(m => m.Id).ValueGeneratedNever();
            b.Property(m => m.Type).HasMaxLength(200).IsRequired();
            b.Property(m => m.RoutingKey).HasMaxLength(200).IsRequired();
            b.Property(m => m.Payload).IsRequired();
            b.Property(m => m.LastError).HasMaxLength(2000);

            b.HasIndex(m => m.CreatedAtUtc)
                .HasDatabaseName("IX_OutboxMessages_Pending")
                .HasFilter("[PublishedAtUtc] IS NULL");
        });
    }
}