using RabbitMQ.Client;

namespace ECommerce.SharedKernel.Messaging;

public static class MessagingTopology
{
    public const string EventsExchange = "ecommerce.events";
    public const string DeadLetterExchange = "ecommerce.dlx";

    public const string OrderCreatedRoutingKey = "order.created";
    public const string OrderStatusChangedRoutingKey = "order.status-changed";

    public const string EmailOrderCreatedQueue = "email.order-created";
    public const string EmailOrderCreatedDlq = "email.order-created.dlq";

    public static readonly (string Queue, int DelayMs)[] EmailRetryQueues =
    [
        ("email.order-created.retry.10s", 10_000),
        ("email.order-created.retry.30s", 30_000)
    ];

    public const string RealtimeOrderEventsQueue = "realtime.order-events";

    public static async Task DeclareAsync(IChannel channel, CancellationToken ct)
    {
        await channel.ExchangeDeclareAsync(
            EventsExchange, ExchangeType.Topic, durable: true, autoDelete: false, cancellationToken: ct);

        await channel.ExchangeDeclareAsync(
            DeadLetterExchange, ExchangeType.Direct, durable: true, autoDelete: false, cancellationToken: ct);

        await channel.QueueDeclareAsync(
            EmailOrderCreatedDlq, durable: true, exclusive: false, autoDelete: false, cancellationToken: ct);

        await channel.QueueBindAsync(
            EmailOrderCreatedDlq, DeadLetterExchange, routingKey: EmailOrderCreatedQueue, cancellationToken: ct);

        var arguments = new Dictionary<string, object?>
        {
            ["x-dead-letter-exchange"] = DeadLetterExchange,
            ["x-dead-letter-routing-key"] = EmailOrderCreatedQueue
        };

        await channel.QueueDeclareAsync(
            EmailOrderCreatedQueue, durable: true, exclusive: false, autoDelete: false,
            arguments: arguments, cancellationToken: ct);

        await channel.QueueBindAsync(
            EmailOrderCreatedQueue, EventsExchange, routingKey: OrderCreatedRoutingKey, cancellationToken: ct);

        foreach (var (queue, delayMs) in EmailRetryQueues)
        {
            var retryArguments = new Dictionary<string, object?>
            {
                ["x-message-ttl"] = delayMs,
                ["x-dead-letter-exchange"] = "",
                ["x-dead-letter-routing-key"] = EmailOrderCreatedQueue
            };

            await channel.QueueDeclareAsync(
                queue, durable: true, exclusive: false, autoDelete: false,
                arguments: retryArguments, cancellationToken: ct);
        }

        var realtimeArguments = new Dictionary<string, object?> { ["x-message-ttl"] = 60_000 };

        await channel.QueueDeclareAsync(
            RealtimeOrderEventsQueue, durable: true, exclusive: false, autoDelete: false,
            arguments: realtimeArguments, cancellationToken: ct);

        await channel.QueueBindAsync(
            RealtimeOrderEventsQueue, EventsExchange, routingKey: OrderCreatedRoutingKey, cancellationToken: ct);

        await channel.QueueBindAsync(
            RealtimeOrderEventsQueue, EventsExchange, routingKey: OrderStatusChangedRoutingKey, cancellationToken: ct);
    }
}
