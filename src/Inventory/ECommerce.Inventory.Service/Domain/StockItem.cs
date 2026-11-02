namespace ECommerce.Inventory.Service.Domain;

internal sealed class StockItem
{
    public Guid ProductId { get; set; }
    public int QuantityOnHand { get; set; }
    public int QuantityReserved { get; set; }
}
