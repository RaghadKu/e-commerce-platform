namespace ECommerce.SharedKernel.Messaging.Events;

public sealed record OrderCreatedItem(Guid ProductId, string ProductName, int Quantity, decimal UnitPrice);

public sealed record OrderCreatedEvent(
    Guid OrderId,
    Guid UserId,
    string CustomerEmail,
    string CustomerName,
    decimal TotalAmount,
    IReadOnlyList<OrderCreatedItem> Items,
    DateTime CreatedAtUtc);
