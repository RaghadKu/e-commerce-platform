using ECommerce.SharedKernel.Api;
using ECommerce.SharedKernel.Caching;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace ECommerce.SharedKernel;

public static class SharedKernelExtensions
{
    public static IServiceCollection AddSharedKernel(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddProblemDetails(options =>
            options.CustomizeProblemDetails = context =>
            {
                context.ProblemDetails.Instance ??= context.HttpContext.Request.Path.Value;
                context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
            });

        services.AddExceptionHandler<GlobalExceptionHandler>();


        services.AddScoped<ICacheStore, RedisCacheStore>();

        var connectionString = configuration.GetConnectionString("Redis");

        // Register StackExchange.Redis multiplexer for advanced features (Pub/Sub, Keyspaces)
        services.AddSingleton<IConnectionMultiplexer>(sp =>
            ConnectionMultiplexer.Connect(connectionString!));

        // Register standard ASP.NET Core distributed cache interface
        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = connectionString;
            options.InstanceName = "EComm_"; // Global prefix to prevent key collisions
        });

        return services;
    }
}
