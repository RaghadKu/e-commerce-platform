using System.Text.Json;
using ECommerce.SharedKernel.Messaging;

namespace ECommerce.Modules.Orders.Infrastructure.Outbox;

internal sealed class OutboxMessage
{
    public Guid Id { get; private set; }
    public string Type { get; private set; } = string.Empty;
    public string RoutingKey { get; private set; } = string.Empty;
    public string Payload { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? PublishedAtUtc { get; private set; }
    public int Attempts { get; private set; }
    public string? LastError { get; private set; }

    public static OutboxMessage Create<T>(string routingKey, T payload) => new()
    {
        Id = Guid.NewGuid(),
        Type = typeof(T).Name,
        RoutingKey = routingKey,
        Payload = JsonSerializer.Serialize(payload, MessagingJson.Options),
        CreatedAtUtc = DateTime.UtcNow
    };
}
