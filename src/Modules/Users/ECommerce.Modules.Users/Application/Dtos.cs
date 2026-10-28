using System.ComponentModel.DataAnnotations;

namespace ECommerce.Modules.Users.Application;

public sealed class RegisterRequest
{
    [Required, StringLength(100, MinimumLength = 2)]
    public string FullName { get; init; } = string.Empty;

    [Required, EmailAddress, StringLength(256)]
    public string Email { get; init; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 8)]
    public string Password { get; init; } = string.Empty;
}

public sealed class LoginRequest
{
    [Required, StringLength(256)]
    public string Email { get; init; } = string.Empty;

    [Required, StringLength(100)]
    public string Password { get; init; } = string.Empty;
}

public sealed class UpdateProfileRequest
{
    [Required, StringLength(100, MinimumLength = 2)]
    public string FullName { get; init; } = string.Empty;
}

public sealed record UserDto(Guid Id, string Email, string FullName, IReadOnlyList<string> Roles, DateTime CreatedAtUtc);

public sealed record LoginResponse(string AccessToken, string TokenType, DateTime ExpiresAtUtc, UserDto User);