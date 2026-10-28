using Microsoft.AspNetCore.Identity;

namespace ECommerce.Modules.Users.Domain;

internal sealed class ApplicationRole : IdentityRole<Guid>
{
    public ApplicationRole() => Id = Guid.NewGuid();
    public ApplicationRole(string roleName) : this() => Name = roleName;
}
