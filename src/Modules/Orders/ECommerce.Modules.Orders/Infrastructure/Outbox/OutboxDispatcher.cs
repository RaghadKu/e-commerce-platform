using System.Text;
using ECommerce.SharedKernel.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace ECommerce.Modules.Orders.Infrastructure.Outbox;

internal sealed class OutboxDispatcher(
    IServiceScopeFactory scopeFactory,
    IConnectionFactory connectionFactory,
    ILogger<OutboxDispatcher> logger) : BackgroundService
{
    private const int BatchSize = 50;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan ErrorDelay = TimeSpan.FromSeconds(5);

    private IConnection? connection;
    private IChannel? channel;
    private bool unroutable;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Outbox dispatcher started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var published = await DispatchBatchAsync(stoppingToken);

                // Nothing to send: wait a little. A full batch: go again at once.
                if (published < BatchSize)
                    await DelayAsync(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Outbox dispatch failed. Trying again in {Seconds}s.", ErrorDelay.TotalSeconds);
                await CloseChannelAsync();
                await DelayAsync(ErrorDelay, stoppingToken);
            }
        }

        await CloseChannelAsync();
        logger.LogInformation("Outbox dispatcher stopped.");
    }

    private async Task<int> DispatchBatchAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();

        var batch = await db.OutboxMessages.AsNoTracking()
            .Where(m => m.PublishedAtUtc == null)
            .OrderBy(m => m.CreatedAtUtc)
            .Take(BatchSize)
            .ToListAsync(ct);

        if (batch.Count == 0)
            return 0;

        var currentChannel = await GetChannelAsync(ct);

        foreach (var message in batch)
        {
            try
            {
                await PublishAsync(currentChannel, message, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Keep the row unsent. Record why, then let the loop wait and retry.
                var error = ex.Message.Length > 1900 ? ex.Message[..1900] : ex.Message;
                await db.OutboxMessages
                    .Where(m => m.Id == message.Id)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(m => m.Attempts, m => m.Attempts + 1)
                        .SetProperty(m => m.LastError, error), ct);
                throw;
            }

            // ExecuteUpdate does not go through SaveChanges, so the heartbeat row is not touched.
            await db.OutboxMessages
                .Where(m => m.Id == message.Id)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(m => m.PublishedAtUtc, (DateTime?)DateTime.UtcNow), ct);

            logger.LogInformation("Published {Type} {MessageId}.", message.Type, message.Id);
        }

        return batch.Count;
    }

    private async Task PublishAsync(IChannel target, OutboxMessage message, CancellationToken ct)
    {
        var properties = new BasicProperties
        {
            Persistent = true,
            MessageId = message.Id.ToString(),
            Type = message.Type,
            ContentType = "application/json"
        };

        unroutable = false;

        await target.BasicPublishAsync(
            exchange: MessagingTopology.EventsExchange,
            routingKey: message.RoutingKey,
            mandatory: true,
            basicProperties: properties,
            body: Encoding.UTF8.GetBytes(message.Payload),
            cancellationToken: ct);

        if (unroutable)
            throw new InvalidOperationException(
                $"No queue is bound to routing key '{message.RoutingKey}'. The message was returned.");
    }

    private async Task<IChannel> GetChannelAsync(CancellationToken ct)
    {
        if (channel is { IsOpen: true })
            return channel;

        await CloseChannelAsync();

        connection = await connectionFactory.CreateConnectionAsync(ct);
        channel = await connection.CreateChannelAsync(
            new CreateChannelOptions(
                publisherConfirmationsEnabled: true,
                publisherConfirmationTrackingEnabled: true),
            ct);

        channel.BasicReturnAsync += (_, _) =>
        {
            unroutable = true;
            return Task.CompletedTask;
        };

        return channel;
    }

    private async Task CloseChannelAsync()
    {
        try { if (channel is not null) await channel.DisposeAsync(); } catch { /* already broken */ }
        try { if (connection is not null) await connection.DisposeAsync(); } catch { /* already broken */ }
        channel = null;
        connection = null;
    }

    private static async Task DelayAsync(TimeSpan delay, CancellationToken ct)
    {
        try { await Task.Delay(delay, ct); }
        catch (OperationCanceledException) { /* the host is stopping */ }
    }
}
