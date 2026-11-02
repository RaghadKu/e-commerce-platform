namespace ECommerce.Inventory.Service.Domain;

internal sealed class StockReservation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrderId { get; set; }
    public ReservationStatus Status { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ReleasedAtUtc { get; set; }
    public List<StockReservationLine> Lines { get; set; } = [];
}

internal enum ReservationStatus { Reserved = 1, Released = 2 }
