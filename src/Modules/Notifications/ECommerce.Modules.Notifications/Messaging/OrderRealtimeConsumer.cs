using System.Text.Json;
using ECommerce.Modules.Notifications.Hubs;
using ECommerce.SharedKernel.Messaging;
using ECommerce.SharedKernel.Messaging.Events;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace ECommerce.Modules.Notifications.Messaging;

internal sealed class OrderRealtimeConsumer(
    IConnectionFactory connectionFactory,
    IHubContext<OrdersHub> customerHub,
    IHubContext<AdminHub> adminHub,
    ILogger<OrderRealtimeConsumer> logger) : BackgroundService
{
    private static readonly TimeSpan RetryConnectDelay = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var connection = await connectionFactory.CreateConnectionAsync(stoppingToken);
                await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

                await MessagingTopology.DeclareAsync(channel, stoppingToken);
                await channel.BasicQosAsync(0, 10, false, stoppingToken);

                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.ReceivedAsync += (_, ea) => HandleAsync(channel, ea, stoppingToken);

                await channel.BasicConsumeAsync(
                    MessagingTopology.RealtimeOrderEventsQueue,
                    autoAck: false,
                    consumer: consumer,
                    cancellationToken: stoppingToken);

                logger.LogInformation("Realtime consumer is listening on {Queue}.", MessagingTopology.RealtimeOrderEventsQueue);

                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Realtime consumer could not connect. Trying again in {Seconds}s.", RetryConnectDelay.TotalSeconds);
                try { await Task.Delay(RetryConnectDelay, stoppingToken); } catch (OperationCanceledException) { break; }
            }
        }
    }

    private async Task HandleAsync(IChannel channel, BasicDeliverEventArgs ea, CancellationToken ct)
    {
        try
        {
            switch (ea.RoutingKey)
            {
                case MessagingTopology.OrderCreatedRoutingKey:
                    {
                        var e = JsonSerializer.Deserialize<OrderCreatedEvent>(ea.Body.Span, MessagingJson.Options)!;

                        await adminHub.Clients.Group(AdminHub.AdminsGroup).SendAsync("OrderCreated", e, ct);
                        break;
                    }

                case MessagingTopology.OrderStatusChangedRoutingKey:
                    {
                        var e = JsonSerializer.Deserialize<OrderStatusChangedEvent>(ea.Body.Span, MessagingJson.Options)!;

                        await customerHub.Clients.Group(OrdersHub.GroupFor(e.UserId)).SendAsync("OrderStatusChanged", e, ct);

                        await adminHub.Clients.Group(AdminHub.AdminsGroup).SendAsync("OrderStatusChanged", e, ct);
                        break;
                    }

                default:
                    logger.LogWarning("Unknown routing key {RoutingKey}. Ignored.", ea.RoutingKey);
                    break;
            }

            await channel.BasicAckAsync(ea.DeliveryTag, multiple: false, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not push realtime event {RoutingKey}. Dropping it.", ea.RoutingKey);
            try { await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false, ct); } catch { }
        }
    }
}