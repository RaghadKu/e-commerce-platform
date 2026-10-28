using ECommerce.Modules.Users.Domain;
using ECommerce.SharedKernel.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ECommerce.Modules.Users.Infrastructure;

internal sealed class UsersDbInitializer(IServiceScopeFactory scopeFactory, IConfiguration configuration)
    : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var services = scope.ServiceProvider;

        var db = services.GetRequiredService<UsersDbContext>();
        if (db.Database.IsRelational())
            await db.Database.MigrateAsync(cancellationToken);      
        else
            await db.Database.EnsureCreatedAsync(cancellationToken); // in-memory

        var roleManager = services.GetRequiredService<RoleManager<ApplicationRole>>();
        foreach (var role in new[] { AppRoles.Admin, AppRoles.Customer })
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new ApplicationRole(role));
        }

        var email = configuration["Seed:AdminEmail"];
        var password = configuration["Seed:AdminPassword"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password)) return;

        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        if (await userManager.FindByEmailAsync(email) is not null) return;

        var admin = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = "System Administrator",
            CreatedAtUtc = DateTime.UtcNow
        };

        var created = await userManager.CreateAsync(admin, password);
        if (!created.Succeeded)
            throw new InvalidOperationException(
                "Could not seed admin: " + string.Join("; ", created.Errors.Select(e => e.Description)));

        await userManager.AddToRoleAsync(admin, AppRoles.Admin);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
