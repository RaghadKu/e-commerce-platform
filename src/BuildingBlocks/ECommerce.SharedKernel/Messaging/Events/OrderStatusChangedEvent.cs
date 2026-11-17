namespace ECommerce.SharedKernel.Messaging.Events;

public sealed record OrderStatusChangedEvent(
    Guid OrderId,
    Guid UserId,
    string OldStatus,
    string NewStatus,
    DateTime ChangedAtUtc);