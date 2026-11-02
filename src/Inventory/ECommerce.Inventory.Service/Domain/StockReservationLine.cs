namespace ECommerce.Inventory.Service.Domain;

internal sealed class StockReservationLine
{
    public Guid ReservationId { get; set; }
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
}
