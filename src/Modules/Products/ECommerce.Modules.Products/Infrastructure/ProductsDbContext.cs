using ECommerce.Modules.Products.Domain;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Modules.Products.Infrastructure;

internal sealed class ProductsDbContext(DbContextOptions<ProductsDbContext> options) : DbContext(options)
{
    public const string Schema = "catalog";

    public DbSet<Product> Products => Set<Product>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.HasDefaultSchema(Schema);

        builder.Entity<Category>(b =>
        {
            b.ToTable("Categories");
            b.HasKey(c => c.Id);
            b.Property(c => c.Name).HasMaxLength(100).IsRequired();
            b.Property(c => c.Description).HasMaxLength(500);

            b.HasIndex(c => c.Name).IsUnique();
        });

        builder.Entity<Product>(b =>
        {
            b.ToTable("Products");
            b.HasKey(p => p.Id);
            b.Property(p => p.Name).HasMaxLength(200).IsRequired();
            b.Property(p => p.Description).HasMaxLength(2000);
            b.Property(p => p.Price).HasPrecision(18, 2);
            b.Property(p => p.Sku).HasMaxLength(50).IsRequired();

            b.HasIndex(p => p.Sku).IsUnique().HasFilter("[IsDeleted] = 0");

            b.HasOne(p => p.Category).WithMany()
                .HasForeignKey(p => p.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasQueryFilter(p => !p.IsDeleted);
        });

        builder.Entity<ProductImage>(b =>
        {
            b.ToTable("ProductImages");
            b.HasKey(i => i.Id);
            b.Property(i => i.StoredFileName).HasMaxLength(100).IsRequired();
            b.Property(i => i.OriginalFileName).HasMaxLength(255).IsRequired();
            b.Property(i => i.ContentType).HasMaxLength(50).IsRequired();

            b.HasOne(i => i.Product).WithMany(p => p.Images)
                .HasForeignKey(i => i.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasQueryFilter(i => !i.Product.IsDeleted);
        });
    }
}