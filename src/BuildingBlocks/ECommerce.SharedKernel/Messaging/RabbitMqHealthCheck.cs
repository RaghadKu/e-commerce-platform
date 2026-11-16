using Microsoft.Extensions.Diagnostics.HealthChecks;
using RabbitMQ.Client;

namespace ECommerce.SharedKernel.Messaging;

internal sealed class RabbitMqHealthCheck(IConnectionFactory factory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = await factory.CreateConnectionAsync(cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("RabbitMQ is not reachable.", ex);
        }
    }
}
