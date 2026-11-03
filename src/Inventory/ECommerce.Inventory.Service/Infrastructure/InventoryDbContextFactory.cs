using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ECommerce.Inventory.Service.Infrastructure;

internal sealed class InventoryDbContextFactory : IDesignTimeDbContextFactory<InventoryDbContext>
{
    public InventoryDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__MainDb")
            ?? "Server=localhost\\SQLEXPRESS;Database=InventoryDb;Trusted_Connection=True;TrustServerCertificate=True";

        return new InventoryDbContext(new DbContextOptionsBuilder<InventoryDbContext>()
            .UseSqlServer(connectionString).Options);
    }
}