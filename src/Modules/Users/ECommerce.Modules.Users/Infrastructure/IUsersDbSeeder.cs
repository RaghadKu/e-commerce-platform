namespace ECommerce.Modules.Users.Infrastructure;

public interface IUsersDbSeeder
{
    Task<IReadOnlyList<Guid>> SeedAsync(
        CancellationToken cancellationToken = default);
}
