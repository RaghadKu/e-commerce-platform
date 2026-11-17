using Microsoft.EntityFrameworkCore;

namespace ECommerce.Modules.Notifications.Infrastructure;

internal sealed class NotificationsDbContext(DbContextOptions<NotificationsDbContext> options)
    : DbContext(options)
{
    public const string Schema = "notifications";

    public DbSet<ProcessedMessage> ProcessedMessages => Set<ProcessedMessage>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.HasDefaultSchema(Schema);

        builder.Entity<ProcessedMessage>(b =>
        {
            b.ToTable("ProcessedMessages");

            b.HasKey(p => new { p.MessageId, p.Consumer });
            b.Property(p => p.MessageId).ValueGeneratedNever();
            b.Property(p => p.Consumer).HasMaxLength(100).IsRequired();
        });
    }
}
