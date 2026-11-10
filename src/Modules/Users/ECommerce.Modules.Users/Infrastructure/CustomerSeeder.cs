using ECommerce.Modules.Users.Domain;
using ECommerce.SharedKernel.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ECommerce.Modules.Users.Infrastructure;

internal sealed class CustomerSeeder(
    UsersDbContext db,
    RoleManager<ApplicationRole> roleManager,
    IPasswordHasher<ApplicationUser> passwordHasher,
    ILookupNormalizer normalizer,
    IConfiguration configuration) : IUsersDbSeeder
{
    private const int CustomerCount = 10_000;
    private const int BatchSize = 500;

    public async Task<IReadOnlyList<Guid>> SeedAsync(
        CancellationToken cancellationToken = default)
    {
        var password = configuration["Seed:CustomerPassword"];

        if (string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException(
                "Seed:CustomerPassword is not configured.");

        var customerRole = await roleManager.FindByNameAsync(AppRoles.Customer)
            ?? throw new InvalidOperationException(
                "Customer role does not exist.");

        var customerIds = new List<Guid>(CustomerCount);
        var totalCreated = 0;

        for (var start = 1; start <= CustomerCount; start += BatchSize)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var end = Math.Min(start + BatchSize - 1, CustomerCount);

            var emails = Enumerable.Range(start, end - start + 1)
                .Select(i => $"customer{i}@example.com")
                .ToArray();

            var normalizedEmails = emails
                .Select(normalizer.NormalizeEmail)
                .ToArray();

            var existingUsers = await db.Users
                .Where(u => normalizedEmails.Contains(u.NormalizedEmail!))
                .ToListAsync(cancellationToken);

            var existingEmails = existingUsers
                .Select(u => u.NormalizedEmail!)
                .ToHashSet();

            customerIds.AddRange(existingUsers.Select(u => u.Id));

            var existingIds = existingUsers
                .Select(u => u.Id)
                .ToArray();

            var usersWithRole = await db.Set<IdentityUserRole<Guid>>()
                .Where(ur => existingIds.Contains(ur.UserId)
                          && ur.RoleId == customerRole.Id)
                .Select(ur => ur.UserId)
                .ToHashSetAsync(cancellationToken);

            var newUsers = new List<ApplicationUser>();
            var roleAssignments = new List<IdentityUserRole<Guid>>();

            foreach (var user in existingUsers)
            {
                if (usersWithRole.Contains(user.Id))
                    continue;

                roleAssignments.Add(new IdentityUserRole<Guid>
                {
                    UserId = user.Id,
                    RoleId = customerRole.Id
                });
            }

            for (var i = start; i <= end; i++)
            {
                var email = $"customer{i}@example.com";
                var normalizedEmail = normalizer.NormalizeEmail(email)!;

                if (existingEmails.Contains(normalizedEmail))
                    continue;

                var customer = new ApplicationUser
                {
                    Id = Guid.NewGuid(),
                    UserName = email,
                    NormalizedUserName = normalizer.NormalizeName(email),
                    Email = email,
                    NormalizedEmail = normalizedEmail,
                    EmailConfirmed = false,
                    FullName = $"Customer {i}",
                    CreatedAtUtc = DateTime.UtcNow,
                    SecurityStamp = Guid.NewGuid().ToString(),
                    ConcurrencyStamp = Guid.NewGuid().ToString()
                };

                customer.PasswordHash =
                    passwordHasher.HashPassword(customer, password);

                newUsers.Add(customer);

                roleAssignments.Add(new IdentityUserRole<Guid>
                {
                    UserId = customer.Id,
                    RoleId = customerRole.Id
                });

                customerIds.Add(customer.Id);
            }

            db.Users.AddRange(newUsers);

            db.Set<IdentityUserRole<Guid>>()
                .AddRange(roleAssignments);

            await db.SaveChangesAsync(cancellationToken);

            totalCreated += newUsers.Count;

            Console.WriteLine(
                $"Processed customers {start}-{end}. " +
                $"Created: {totalCreated}");

            db.ChangeTracker.Clear();
        }

        Console.WriteLine(
            $"Customer seeding completed. New customers: {totalCreated}");

        return customerIds;
    }
}
