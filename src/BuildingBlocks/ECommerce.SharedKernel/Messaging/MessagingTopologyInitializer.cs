using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace ECommerce.SharedKernel.Messaging;

internal sealed class MessagingTopologyInitializer(
    IConnectionFactory factory,
    ILogger<MessagingTopologyInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var connection = await factory.CreateConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

        await MessagingTopology.DeclareAsync(channel, cancellationToken);

        logger.LogInformation("RabbitMQ exchanges and queues are ready.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
