namespace ECommerce.Modules.Orders.Domain;

public class OrderItem
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public Guid ProductId { get; set; }
    public int Qty { get; set; }
    public decimal Price { get; set; }
    public virtual Order Order { get; set; }
}
