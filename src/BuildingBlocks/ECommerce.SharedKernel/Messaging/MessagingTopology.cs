using RabbitMQ.Client;

namespace ECommerce.SharedKernel.Messaging;

public static class MessagingTopology
{
    public const string EventsExchange = "ecommerce.events";
    public const string DeadLetterExchange = "ecommerce.dlx";

    public const string OrderCreatedRoutingKey = "order.created";

    public const string EmailOrderCreatedQueue = "email.order-created";
    public const string EmailOrderCreatedDlq = "email.order-created.dlq";

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
    }
}
