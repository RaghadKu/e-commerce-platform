using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ECommerce.Modules.Products.Infrastructure;

internal sealed class ProductsDbContextFactory : IDesignTimeDbContextFactory<ProductsDbContext>
{
    public ProductsDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__MainDb")
            ?? "Server=(localdb)\\MSSQLLocalDB;Database=MainDb;Trusted_Connection=True;TrustServerCertificate=True";

        var options = new DbContextOptionsBuilder<ProductsDbContext>()
            .UseSqlServer(connectionString, sql =>
                sql.MigrationsHistoryTable("__EFMigrationsHistory", ProductsDbContext.Schema))
            .Options;

        return new ProductsDbContext(options);
    }
}
