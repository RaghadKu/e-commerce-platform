using ECommerce.Modules.Users.Domain;
using ECommerce.SharedKernel.Exceptions;
using Microsoft.AspNetCore.Identity;

namespace ECommerce.Modules.Users.Application;

public interface IUserService
{
    Task<UserDto> GetByIdAsync(Guid id, Guid requesterId, bool requesterIsAdmin);
    Task<UserDto> UpdateProfileAsync(Guid userId, UpdateProfileRequest request);
}

internal sealed class UserService(UserManager<ApplicationUser> userManager) : IUserService
{
    public async Task<UserDto> GetByIdAsync(Guid id, Guid requesterId, bool requesterIsAdmin)
    {
        // Ownership check BEFORE the database lookup: a non-owner can't learn whether the id exists.
        if (id != requesterId && !requesterIsAdmin)
            throw new ForbiddenException("You do not have access to this user.");

        var user = await userManager.FindByIdAsync(id.ToString())
                   ?? throw new NotFoundException("User not found.");

        return AuthService.ToDto(user, await userManager.GetRolesAsync(user));
    }

    public async Task<UserDto> UpdateProfileAsync(Guid userId, UpdateProfileRequest request)
    {
        var user = await userManager.FindByIdAsync(userId.ToString())
                   ?? throw new NotFoundException("User not found.");

        user.FullName = request.FullName.Trim();
        (await userManager.UpdateAsync(user)).ThrowIfFailed();

        return AuthService.ToDto(user, await userManager.GetRolesAsync(user));
    }
}
