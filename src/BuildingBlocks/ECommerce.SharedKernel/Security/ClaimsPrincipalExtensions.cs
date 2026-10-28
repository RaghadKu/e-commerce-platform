using System.Security.Claims;
using ECommerce.SharedKernel.Exceptions;

namespace ECommerce.SharedKernel.Security;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirst("sub")?.Value, out var id)
            ? id
            : throw new UnauthorizedException("The token does not contain a valid user id.");

    public static bool IsAdmin(this ClaimsPrincipal user) => user.IsInRole(AppRoles.Admin);
}
