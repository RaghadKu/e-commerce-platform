using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ECommerce.Modules.Orders.Domain;

public enum OrderStatus
{
    Draft,       // Basket like
    Pending,     // After submit waiting payment
    Confirmed,   // Payment has been successfully verified
    Processing,  // Preparing the order.
    Shipped,
    Delivered,
    Cancelled
}

public class Order
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public List<OrderItem> Items { get; set; } = [];
    public OrderStatus Status { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public static Order Create(Guid userId)
    {
        return new Order
        {
            UserId = userId,
            Status = OrderStatus.Draft,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
    }
}
