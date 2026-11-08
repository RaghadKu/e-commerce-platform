namespace ECommerce.Modules.Users.Infrastructure;

public interface IUsersDbSeeder
{
    Task SeedAsync(CancellationToken cancellationToken);
}
