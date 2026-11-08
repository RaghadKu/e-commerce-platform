using ECommerce.Inventory.Contracts;
using ECommerce.Inventory.Service.Domain;
using ECommerce.Inventory.Service.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;

namespace ECommerce.Inventory.Service.Application;

internal sealed record StockSnapshot(Guid ProductId, int OnHand, int Reserved);
internal sealed record Shortage(Guid ProductId, int Requested, int Available);
internal sealed record ReserveResult(ReserveStatus Status, IReadOnlyList<Shortage> Shortages);

internal sealed class StockManager(InventoryDbContext db)
{
    public async Task<IReadOnlyList<StockSnapshot>> CheckAsync(IReadOnlyCollection<Guid> productIds, CancellationToken ct)
    {
        var ids = productIds.ToList();

        var found = await db.StockItems
            .AsNoTracking()
            .Where(s => ids.Contains(s.ProductId))
            .ToDictionaryAsync(s => s.ProductId, ct);

        return ids.Select(id => found.TryGetValue(id, out var s)
            ? new StockSnapshot(id, s.QuantityOnHand, s.QuantityReserved)
            : new StockSnapshot(id, 0, 0)).ToList();
    }

    public async Task<ReserveResult> ReserveAsync(
        Guid orderId, IReadOnlyList<(Guid ProductId, int Quantity)> lines, CancellationToken ct)
    {
        var ordered = lines.GroupBy(l => l.ProductId)
            .Select(g => (ProductId: g.Key, Quantity: g.Sum(x => x.Quantity)))
            .OrderBy(l => l.ProductId)
            .ToList();

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        db.Reservations.Add(new StockReservation
        {
            OrderId = orderId,
            Status = ReservationStatus.Reserved,
            CreatedAtUtc = DateTime.UtcNow,
            Lines = ordered.Select(l => new StockReservationLine { ProductId = l.ProductId, Quantity = l.Quantity }).ToList()
        });

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            await tx.RollbackAsync(ct);

            db.ChangeTracker.Clear();

            var existing = await db.Reservations
                .AsNoTracking()
                .FirstAsync(r => r.OrderId == orderId, ct);

            return new ReserveResult(
                existing.Status == ReservationStatus.Reserved ? ReserveStatus.Reserved : ReserveStatus.AlreadyReleased, []);
        }

        foreach (var (productId, quantity) in ordered)
        {
            var updated = await db.StockItems
                .Where(s => s.ProductId == productId && s.QuantityOnHand - s.QuantityReserved >= quantity)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.QuantityReserved, x => x.QuantityReserved + quantity), ct);

            if (updated == 0)
            {
                await tx.RollbackAsync(ct);
                db.ChangeTracker.Clear();
                return new ReserveResult(ReserveStatus.InsufficientStock, await ShortagesAsync(ordered, ct));
            }
        }

        await tx.CommitAsync(ct);
        return new ReserveResult(ReserveStatus.Reserved, []);
    }

    public async Task<ReleaseStatus> ReleaseAsync(Guid orderId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var reservation = await db.Reservations
            .AsNoTracking()
            .Include(r => r.Lines)
            .FirstOrDefaultAsync(r => r.OrderId == orderId, ct);

        if (reservation is null)
        {
            db.Reservations.Add(new StockReservation
            {
                OrderId = orderId,
                Status = ReservationStatus.Released,
                CreatedAtUtc = DateTime.UtcNow,
                ReleasedAtUtc = DateTime.UtcNow
            });

            try
            {
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return ReleaseStatus.NotFound;
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex))
            {
                await tx.RollbackAsync(ct);
                db.ChangeTracker.Clear();
                return await ReleaseAsync(orderId, ct);
            }
        }

        var now = DateTime.UtcNow;

        var flipped = await db.Reservations
            .Where(r => r.Id == reservation.Id && r.Status == ReservationStatus.Reserved)
            .ExecuteUpdateAsync(r => r
                .SetProperty(x => x.Status, ReservationStatus.Released)
                .SetProperty(x => x.ReleasedAtUtc, now), ct);

        if (flipped == 0)
        {
            await tx.RollbackAsync(ct);
            return ReleaseStatus.AlreadyReleased;
        }

        foreach (var line in reservation.Lines.OrderBy(l => l.ProductId))
        {
            var productId = line.ProductId;
            var quantity = line.Quantity;
            await db.StockItems
                .Where(s => s.ProductId == productId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.QuantityReserved, x => x.QuantityReserved - quantity), ct);
        }

        await tx.CommitAsync(ct);
        return ReleaseStatus.Released;
    }

    public async Task<bool> SetStockAsync(Guid productId, int quantity, CancellationToken ct)
    {
        var updated = await db.StockItems
            .Where(s => s.ProductId == productId && s.QuantityReserved <= quantity)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.QuantityOnHand, quantity), ct);

        if (updated == 1)
            return true;

        if (await db.StockItems.AnyAsync(s => s.ProductId == productId, ct))
            return false;

        db.StockItems.Add(new StockItem { ProductId = productId, QuantityOnHand = quantity });
        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            db.ChangeTracker.Clear();
            return await SetStockAsync(productId, quantity, ct);
        }
    }

    public async Task<int> SeedMissingProductsAsync(
        IReadOnlyCollection<Guid> productIds,
        CancellationToken ct)
    {
        var createdCount = 0;

        foreach (var batch in productIds.Distinct().Chunk(500))
        {
            var existingIds = await db.StockItems
                .Where(s => batch.Contains(s.ProductId))
                .Select(s => s.ProductId)
                .ToListAsync(ct);

            var existingSet = existingIds.ToHashSet();

            var newItems = batch
                .Where(id => !existingSet.Contains(id))
                .Select(id => new StockItem
                {
                    ProductId = id,
                    QuantityOnHand = Random.Shared.Next(10, 101),
                    QuantityReserved = 0
                })
                .ToList();

            if (newItems.Count == 0)
                continue;

            db.StockItems.AddRange(newItems);

            createdCount += await db.SaveChangesAsync(ct);
        }

        return createdCount;
    }

    private async Task<IReadOnlyList<Shortage>> ShortagesAsync(
        IReadOnlyList<(Guid ProductId, int Quantity)> lines, CancellationToken ct)
    {
        var levels = (await CheckAsync(lines.Select(l => l.ProductId).ToList(), ct)).ToDictionary(l => l.ProductId);

        return lines
            .Select(l => new Shortage(l.ProductId, l.Quantity, levels[l.ProductId].OnHand - levels[l.ProductId].Reserved))
            .Where(s => s.Available < s.Requested)
            .ToList();
    }

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is SqlException { Number: 2601 or 2627 };
}
