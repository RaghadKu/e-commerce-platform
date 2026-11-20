using ECommerce.Modules.Notifications.Email;
using ECommerce.Modules.Notifications.Infrastructure;
using ECommerce.Modules.Notifications.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerce.Modules.Notifications;

public static class NotificationsModule
{
    public static IServiceCollection AddNotificationsModule(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<EmailOptions>()
            .Bind(configuration.GetSection(EmailOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddDbContext<NotificationsDbContext>((sp, options) =>
        {
            var connectionString = sp.GetRequiredService<IConfiguration>().GetConnectionString("MainDb")
                ?? throw new InvalidOperationException("Connection string 'MainDb' is not configured.");

            options.UseSqlServer(connectionString, sql =>
                sql.MigrationsHistoryTable("__EFMigrationsHistory", NotificationsDbContext.Schema));
        });

        services.AddScoped<IEmailSender, MailKitEmailSender>();


        var redisConnectionString = configuration.GetConnectionString("Redis");
        var signalRBuilder = services.AddSignalR();

        if (!string.IsNullOrWhiteSpace(redisConnectionString))
        {
            signalRBuilder.AddStackExchangeRedis(redisConnectionString);
        }

        // Order matters: migrate first, then start the consumers.
        services.AddHostedService<NotificationsDbInitializer>();
        services.AddHostedService<OrderCreatedEmailConsumer>();
        services.AddHostedService<OrderRealtimeConsumer>();

        return services;
    }
}