using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;

namespace ECommerce.SharedKernel.Persistence;

public sealed class HeartbeatInterceptor(ConsistencyContext consistency) : SaveChangesInterceptor
{
    private readonly ConditionalWeakTable<DbContext, IDbContextTransaction> owned = new();

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        var db = eventData.Context;
        if (db is not null && db.Database.CurrentTransaction is null && db.ChangeTracker.HasChanges())
            owned.AddOrUpdate(db, await db.Database.BeginTransactionAsync(cancellationToken));

        return result;
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        var db = eventData.Context;
        if (db is null) return result;

        if (owned.TryGetValue(db, out var mine))
        {
            owned.Remove(db);
            await using (mine)
            {
                long? token = result > 0 ? await BumpAsync(db, cancellationToken) : null;
                await mine.CommitAsync(cancellationToken);

                if (token is not null) consistency.WriteToken = token;
            }
        }
        else if (result > 0 && db.Database.CurrentTransaction is not null)
        {
            consistency.WriteToken = await BumpAsync(db, cancellationToken);
        }

        return result;
    }

    public override async Task SaveChangesFailedAsync(
        DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is { } db && owned.TryGetValue(db, out var mine))
        {
            owned.Remove(db);
            await mine.RollbackAsync(cancellationToken);
            await mine.DisposeAsync();
        }
    }

    private static async Task<long> BumpAsync(DbContext db, CancellationToken ct)
    {
        var connection = db.Database.GetDbConnection();
        await using var cmd = connection.CreateCommand();
        cmd.Transaction = db.Database.CurrentTransaction!.GetDbTransaction();
        cmd.CommandText =
            "UPDATE consistency.Heartbeat SET Beat = Beat + 1 " +
            "OUTPUT CONVERT(bigint, inserted.RowVer) WHERE Id = 1";

        var value = await cmd.ExecuteScalarAsync(ct);
        return value is long version
            ? version
            : throw new InvalidOperationException("Heartbeat row is missing. Run Step 0.");
    }
}