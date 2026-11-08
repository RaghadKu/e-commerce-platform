using ECommerce.Modules.Users.Domain;
using ECommerce.SharedKernel.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Data;

namespace ECommerce.Modules.Users.Infrastructure;

internal class AdminsDbSeeder(IServiceScopeFactory scopeFactory, IConfiguration configuration) : IHostedService
{
    private const int AdminCount = 50;
    private const int CustomerCount = 100_000;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();

        var services = scope.ServiceProvider;

        var roleManager = services
            .GetRequiredService<RoleManager<ApplicationRole>>();

        var userManager = services
            .GetRequiredService<UserManager<ApplicationUser>>();

        await SeedRolesAsync(roleManager);

        await SeedAdminsAsync(userManager, configuration);
    }

    private static async Task SeedRolesAsync(RoleManager<ApplicationRole> roleManager)
    {
        foreach(var role in new[] { AppRoles.Admin, AppRoles.Customer })
        {
            if(await roleManager.RoleExistsAsync(role))
            {
                continue;
            }

            var result = await roleManager.CreateAsync(new ApplicationRole(role));

            if(!result.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Could not create role '{role}': " +
                    string.Join("; ", result.Errors.Select(e => e.Description)));
            }
        }
    }

    private static async Task SeedAdminsAsync(UserManager<ApplicationUser> userManager, IConfiguration configuration)
    {
        var password = configuration["Seed:AdminPassword"];

        if (string.IsNullOrWhiteSpace(password))
            return;

        for (var i = 1; i <= AdminCount; i++)
        {
            var email = $"admin{i}@example.com";

            var admin = await userManager.FindByEmailAsync(email);

            if (admin is null)
            {
                admin = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    FullName = $"System Administrator {i}",
                    CreatedAtUtc = DateTime.UtcNow
                };

                var createResult = await userManager.CreateAsync(
                    admin, password);

                if (!createResult.Succeeded)
                {
                    throw new InvalidOperationException(
                        $"Could not create admin '{email}': " +
                        string.Join("; ",
                            createResult.Errors.Select(e => e.Description)));
                }
            }

            if (!await userManager.IsInRoleAsync(admin, AppRoles.Admin))
            {
                var roleResult = await userManager.AddToRoleAsync(
                    admin, AppRoles.Admin);

                if (!roleResult.Succeeded)
                {
                    throw new InvalidOperationException(
                        $"Could not assign Admin role to '{email}': " +
                        string.Join("; ",
                            roleResult.Errors.Select(e => e.Description)));
                }
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
