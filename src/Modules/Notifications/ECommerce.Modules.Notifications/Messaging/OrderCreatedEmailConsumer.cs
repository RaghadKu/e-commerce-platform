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
                // After this first connect, the client library reconnects by itself if the network drops.
                await using var connection = await connectionFactory.CreateConnectionAsync(stoppingToken);
                await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

                // Safe to repeat. Makes sure the queue exists even if we start first.
                await MessagingTopology.DeclareAsync(channel, stoppingToken);

                // Prefetch 1: RabbitMQ sends the next message only after we ACK the current one.
                await channel.BasicQosAsync(0, 1, false, stoppingToken);

                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.ReceivedAsync += (_, ea) => HandleAsync(channel, ea, stoppingToken);

                await channel.BasicConsumeAsync(
                    MessagingTopology.EmailOrderCreatedQueue,
                    autoAck: false,              // WE decide when the message is done
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

    private async Task HandleAsync(IChannel channel, BasicDeliverEventArgs ea, CancellationToken ct)
    {
        try
        {
            // A message without a valid id or body can never succeed. Do not retry it.
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

            // 1. Duplicate check.
            var already = await db.ProcessedMessages
                .AnyAsync(p => p.MessageId == messageId && p.Consumer == ConsumerName, ct);

            if (already)
            {
                logger.LogInformation("Message {MessageId} was already processed. Skipping.", messageId);
                await channel.BasicAckAsync(ea.DeliveryTag, multiple: false, ct);
                return;
            }

            // 2. Send the email. If this throws, we do NOT ACK.
            var (subject, html) = OrderConfirmationEmail.Build(orderEvent);
            await sender.SendAsync(orderEvent.CustomerEmail, orderEvent.CustomerName, subject, html, ct);

            // 3. Record it, so a repeated message is skipped.
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

            // 4. Only now tell RabbitMQ we are done.
            await channel.BasicAckAsync(ea.DeliveryTag, multiple: false, ct);
            logger.LogInformation("Message {MessageId} processed for order {OrderId}.", messageId, orderEvent.OrderId);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // The host is stopping. No ACK: RabbitMQ will deliver this message again.
        }
        catch (Exception ex)
        {
            // TEMPORARY (Step 4 adds retries): a failed message goes straight to the DLQ.
            logger.LogError(ex, "Processing failed. Sending the message to the dead letter queue.");
            try { await RejectAsync(channel, ea, ct); } catch { /* channel is gone, RabbitMQ requeues it */ }
        }
    }

    // NACK with requeue=false: RabbitMQ moves the message to the dead letter exchange.
    private static ValueTask RejectAsync(IChannel channel, BasicDeliverEventArgs ea, CancellationToken ct)
        => channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false, ct);

    private static OrderCreatedEvent? TryRead(ReadOnlySpan<byte> body)
    {
        try { return JsonSerializer.Deserialize<OrderCreatedEvent>(body, MessagingJson.Options); }
        catch (JsonException) { return null; }
    }
}
