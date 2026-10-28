using ECommerce.Modules.Users.Domain;
using ECommerce.Modules.Users.Infrastructure;
using ECommerce.SharedKernel.Exceptions;
using ECommerce.SharedKernel.Security;
using Microsoft.AspNetCore.Identity;

namespace ECommerce.Modules.Users.Application;

public interface IAuthService
{
    Task<UserDto> RegisterAsync(RegisterRequest request);
    Task<LoginResponse> LoginAsync(LoginRequest request);
}

internal sealed class AuthService(UserManager<ApplicationUser> userManager, JwtTokenGenerator tokenGenerator)
    : IAuthService
{
    private const string LockedMessage =
        "Too many failed login attempts. The account is temporarily locked. Try again later.";

    public async Task<UserDto> RegisterAsync(RegisterRequest request)
    {
        var email = request.Email.Trim();
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = request.FullName.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        };

        (await userManager.CreateAsync(user, request.Password)).ThrowIfFailed();

        var addRole = await userManager.AddToRoleAsync(user, AppRoles.Customer);
        if (!addRole.Succeeded)
        {
            await userManager.DeleteAsync(user);
            addRole.ThrowIfFailed();
        }

        return ToDto(user, [AppRoles.Customer]);
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request)
    {
        var user = await userManager.FindByEmailAsync(request.Email.Trim())
                   ?? throw new UnauthorizedException("Invalid email or password.");

        if (await userManager.IsLockedOutAsync(user))
            throw new LockedException(LockedMessage);

        if (!await userManager.CheckPasswordAsync(user, request.Password))
        {
            await userManager.AccessFailedAsync(user);

            if (await userManager.IsLockedOutAsync(user))
                throw new LockedException(LockedMessage);

            throw new UnauthorizedException("Invalid email or password.");
        }

        await userManager.ResetAccessFailedCountAsync(user);

        var roles = await userManager.GetRolesAsync(user);
        var (token, expires) = tokenGenerator.Generate(user, roles);

        return new LoginResponse(token, "Bearer", expires, ToDto(user, roles));
    }

    internal static UserDto ToDto(ApplicationUser user, IEnumerable<string> roles) =>
        new(user.Id, user.Email ?? string.Empty, user.FullName, roles.Order().ToList(), user.CreatedAtUtc);
}
