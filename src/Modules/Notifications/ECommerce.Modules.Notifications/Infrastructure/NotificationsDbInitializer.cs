using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ECommerce.Modules.Notifications.Infrastructure;

internal sealed class NotificationsDbInitializer(IServiceScopeFactory scopeFactory) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();

        if (db.Database.IsRelational())
            await db.Database.MigrateAsync(cancellationToken);
        else
            await db.Database.EnsureCreatedAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
