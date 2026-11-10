using ECommerce.Modules.Orders.Application.Services;
using ECommerce.Modules.Orders.Infrastructure;
using ECommerce.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerce.Modules.Orders;

public static class OrdersModule
{
    public static IServiceCollection AddOrdersModule(
        this IServiceCollection services, IConfiguration configuration)
    {
        // Services will be registered here.
        services.AddHttpContextAccessor();
        services.AddScoped<IOrderService, OrderService>();
        services.AddDbContext<OrdersDbContext>((sp, options) =>
        {
            var connectionString = sp.GetRequiredService<IConfiguration>().GetConnectionString("MainDb")
                ?? throw new InvalidOperationException("Connection string 'MainDb' is not configured.");

            options.UseSqlServer(connectionString, sql
                => sql.MigrationsHistoryTable("__EFMigrationsHistory", OrdersDbContext.Schema));

            options.AddHeartbeat(sp);
        });

        services.AddDbContext<OrdersReadDbContext>((sp, options) =>
        {
            options.UseSqlServer(ReadReplicaSettings.GetReadConnectionString(
                sp.GetRequiredService<IConfiguration>()));
        });

        services.AddHostedService<OrdersDbInitializer>();

        return services;
    }
}
