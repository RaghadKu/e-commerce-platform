using ECommerce.SharedKernel.Exceptions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Modules.Products.Application;

internal static class DbContextExtensions
{
    public static async Task SaveOrConflictAsync(this DbContext db, CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            throw new ConflictException("The change conflicts with existing data (duplicate name or SKU).");
        }
    }
}
