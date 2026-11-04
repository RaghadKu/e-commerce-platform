using ECommerce.Modules.Orders.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ECommerce.Modules.Orders.Application;

public class OrderDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public List<OrderItemDto> Items { get; set; } = [];
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public static OrderDto Create(Order order)
    {
        return new OrderDto
        {
            Id = order.Id,
            UserId = order.UserId,
            Items = order.Items.Select(i => OrderItemDto.Create(i)).ToList(),
            Status = order.Status.ToString(),
            CreatedAtUtc = order.CreatedAtUtc,
            UpdatedAtUtc = order.UpdatedAtUtc
        };
    }
}

public class OrderItemDto
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public Guid ProductId { get; set; }
    public int Qty { get; set; }
    public decimal Price { get; set; }

    public static OrderItemDto Create(OrderItem orderItem)
    {
        return new OrderItemDto
        {
            Id = orderItem.Id,
            OrderId = orderItem.OrderId,
            ProductId = orderItem.ProductId,
            Qty = orderItem.Qty,
            Price = orderItem.Price,
        };
    }
}

public record AddOrderItemRequest(Guid ProductId, int Qty);
public record UpdateOrderItemRequest(Guid OrderItemId, int Qty);

