using System.Text.Json;
using ECommerce.Modules.Notifications.Email;
using ECommerce.Modules.Notifications.Infrastructure;
using ECommerce.SharedKernel.Messaging;
using ECommerce.SharedKernel.Messaging.Events;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace ECommerce.Modules.Notifications.Messaging;

internal sealed class OrderCreatedEmailConsumer(
    IServiceScopeFactory scopeFactory,
    IConnectionFactory connectionFactory,
    IConfiguration configuration,
    ILogger<OrderCreatedEmailConsumer> logger) : BackgroundService
{
    private const string ConsumerName = "email.order-created";
    private const string RetryCountHeader = "x-retry-count";
    private const string LastErrorHeader = "x-last-error";
    private static readonly TimeSpan RetryConnectDelay = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue("Messaging:EmailWorkerEnabled", true))
        {
            logger.LogWarning("Email worker is DISABLED. Messages will wait in RabbitMQ.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var connection = await connectionFactory.CreateConnectionAsync(stoppingToken);
                await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

                await using var publishChannel = await connection.CreateChannelAsync(
                    new CreateChannelOptions(
                        publisherConfirmationsEnabled: true,
                        publisherConfirmationTrackingEnabled: true),
                    stoppingToken);

                await MessagingTopology.DeclareAsync(channel, stoppingToken);
                await channel.BasicQosAsync(0, 1, false, stoppingToken);

                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.ReceivedAsync += (_, ea) => HandleAsync(channel, publishChannel, ea, stoppingToken);

                await channel.BasicConsumeAsync(
                    MessagingTopology.EmailOrderCreatedQueue,
                    autoAck: false,
                    consumer: consumer,
                    cancellationToken: stoppingToken);

                logger.LogInformation("Email worker is listening on {Queue}.", MessagingTopology.EmailOrderCreatedQueue);

                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Email worker could not connect. Trying again in {Seconds}s.", RetryConnectDelay.TotalSeconds);
                try { await Task.Delay(RetryConnectDelay, stoppingToken); } catch (OperationCanceledException) { break; }
            }
        }

        logger.LogInformation("Email worker stopped.");
    }

    private async Task HandleAsync(
        IChannel channel, IChannel publishChannel, BasicDeliverEventArgs ea, CancellationToken ct)
    {
        try
        {
            if (!Guid.TryParse(ea.BasicProperties.MessageId, out var messageId))
            {
                logger.LogError("Message has no valid MessageId. Rejecting.");
                await RejectAsync(channel, ea, ct);
                return;
            }

            var orderEvent = TryRead(ea.Body.Span);
            if (orderEvent is null || string.IsNullOrWhiteSpace(orderEvent.CustomerEmail))
            {
                logger.LogError("Message {MessageId} is invalid or has no customer email. Rejecting.", messageId);
                await RejectAsync(channel, ea, ct);
                return;
            }

            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
            var sender = scope.ServiceProvider.GetRequiredService<IEmailSender>();

            var already = await db.ProcessedMessages
                .AnyAsync(p => p.MessageId == messageId && p.Consumer == ConsumerName, ct);

            if (already)
            {
                logger.LogInformation("Message {MessageId} was already processed. Skipping.", messageId);
                await channel.BasicAckAsync(ea.DeliveryTag, multiple: false, ct);
                return;
            }

            var (subject, html) = OrderConfirmationEmail.Build(orderEvent);
            await sender.SendAsync(orderEvent.CustomerEmail, orderEvent.CustomerName, subject, html, ct);

            db.ProcessedMessages.Add(new ProcessedMessage
            {
                MessageId = messageId,
                Consumer = ConsumerName,
                ProcessedAtUtc = DateTime.UtcNow
            });

            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2627 or 2601 })
            {
                // Another copy recorded it first. Fine.
            }

            await channel.BasicAckAsync(ea.DeliveryTag, multiple: false, ct);
            logger.LogInformation("Message {MessageId} processed for order {OrderId}.", messageId, orderEvent.OrderId);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            await HandleFailureAsync(channel, publishChannel, ea, ex, ct);
        }
    }

    /// <summary>
    /// Failure 1 and 2: copy the message to a wait queue (10 s, then 30 s).
    /// Failure 3: copy it to the dead letter exchange (the DLQ).
    /// The original is ACKed only after the copy is safely stored.
    /// </summary>
    private async Task HandleFailureAsync(
        IChannel channel, IChannel publishChannel, BasicDeliverEventArgs ea, Exception error, CancellationToken ct)
    {
        var retriesSoFar = GetRetryCount(ea.BasicProperties);
        var reason = error.Message.Length > 500 ? error.Message[..500] : error.Message;

        try
        {
            if (retriesSoFar < MessagingTopology.EmailRetryQueues.Length)
            {
                var (queue, delayMs) = MessagingTopology.EmailRetryQueues[retriesSoFar];

                await PublishCopyAsync(publishChannel, ea, exchange: "", routingKey: queue, retriesSoFar + 1, reason, ct);

                logger.LogWarning(error,
                    "Attempt {Attempt} failed for message {MessageId}. Retrying in {Seconds}s.",
                    retriesSoFar + 1, ea.BasicProperties.MessageId, delayMs / 1000);
            }
            else
            {
                await PublishCopyAsync(
                    publishChannel, ea,
                    exchange: MessagingTopology.DeadLetterExchange,
                    routingKey: MessagingTopology.EmailOrderCreatedQueue,
                    retriesSoFar, reason, ct);

                logger.LogError(error,
                    "Message {MessageId} failed {Attempts} times. Moved to the dead letter queue.",
                    ea.BasicProperties.MessageId, retriesSoFar + 1);
            }

            await channel.BasicAckAsync(ea.DeliveryTag, multiple: false, ct);
        }
        catch (Exception publishError)
        {
            logger.LogError(publishError, "Could not republish message {MessageId}. Requeueing it.", ea.BasicProperties.MessageId);
            try { await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: true, ct); }
            catch { /* channel is gone, RabbitMQ requeues it */ }
        }
    }

    private static Task PublishCopyAsync(
        IChannel publishChannel, BasicDeliverEventArgs ea,
        string exchange, string routingKey, int retryCount, string reason, CancellationToken ct)
    {
        var properties = new BasicProperties
        {
            Persistent = true,
            MessageId = ea.BasicProperties.MessageId,
            Type = ea.BasicProperties.Type,
            ContentType = ea.BasicProperties.ContentType,
            Headers = new Dictionary<string, object?>
            {
                [RetryCountHeader] = retryCount,
                [LastErrorHeader] = reason
            }
        };

        return publishChannel.BasicPublishAsync(
            exchange, routingKey, mandatory: false, basicProperties: properties,
            body: ea.Body, cancellationToken: ct).AsTask();
    }

    private static int GetRetryCount(IReadOnlyBasicProperties properties)
    {
        if (properties.Headers is not null
            && properties.Headers.TryGetValue(RetryCountHeader, out var value)
            && value is not null)
        {
            try { return Convert.ToInt32(value); } catch { return 0; }
        }

        return 0;
    }

    private static ValueTask RejectAsync(IChannel channel, BasicDeliverEventArgs ea, CancellationToken ct)
        => channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false, ct);

    private static OrderCreatedEvent? TryRead(ReadOnlySpan<byte> body)
    {
        try { return JsonSerializer.Deserialize<OrderCreatedEvent>(body, MessagingJson.Options); }
        catch (JsonException) { return null; }
    }
}