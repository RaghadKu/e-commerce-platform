using Microsoft.AspNetCore.Identity;

namespace ECommerce.Modules.Users.Domain;

internal sealed class ApplicationUser : IdentityUser<Guid>
{
    public ApplicationUser() => Id = Guid.NewGuid();

    public string FullName { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}
