using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ECommerce.SharedKernel.Persistence;

public interface IReadRouter
{
    Task<bool> UseReplicaAsync(CancellationToken ct);
}

internal sealed class ReadRouter(
    IConfiguration configuration,
    ConsistencyContext consistency,
    ReplicaCircuitBreaker breaker,
    ILogger<ReadRouter> logger) : IReadRouter
{
    private bool? decision;

    public async Task<bool> UseReplicaAsync(CancellationToken ct)
        => decision ??= await DecideAsync(ct);

    private async Task<bool> DecideAsync(CancellationToken ct)
    {
        if (!configuration.GetValue<bool>("ReadReplica:Enabled")) return false;

        if (consistency.WriteToken is not null) return false;

        if (breaker.IsOpen) return false;

        var required = consistency.RequiredToken ?? 0;

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(2));

            await using var connection = new SqlConnection(ReadReplicaSettings.GetReadConnectionString(configuration));
            await connection.OpenAsync(timeout.Token);

            await using var cmd = new SqlCommand(
                "SELECT CONVERT(bigint, RowVer) FROM consistency.Heartbeat WHERE Id = 1", connection);
            var value = await cmd.ExecuteScalarAsync(timeout.Token);

            breaker.Success();
            return value is long replicaVersion && replicaVersion >= required;
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            breaker.Failure();
            logger.LogWarning(ex, "Replica check failed. Reading from the primary.");
            return false;
        }
    }
}

public static class ReadConsistencyExtensions
{
    public static IServiceCollection AddReadConsistency(this IServiceCollection services)
    {
        services.AddScoped<ConsistencyContext>();
        services.AddScoped<HeartbeatInterceptor>();
        services.AddScoped<IReadRouter, ReadRouter>();
        services.AddSingleton<ReplicaCircuitBreaker>();
        return services;
    }

    public static Microsoft.EntityFrameworkCore.DbContextOptionsBuilder AddHeartbeat(
        this Microsoft.EntityFrameworkCore.DbContextOptionsBuilder options, IServiceProvider sp)
        => options.AddInterceptors(sp.GetRequiredService<HeartbeatInterceptor>());
}