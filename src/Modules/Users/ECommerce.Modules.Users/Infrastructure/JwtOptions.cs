using System.ComponentModel.DataAnnotations;

namespace ECommerce.Modules.Users.Infrastructure;

internal sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required] public string Issuer { get; set; } = string.Empty;
    [Required] public string Audience { get; set; } = string.Empty;
    [Required, MinLength(32)] public string Key { get; set; } = string.Empty;
    [Range(1, 1440)] public int ExpiryMinutes { get; set; } = 60;
}

internal sealed class LoginSecurityOptions
{
    public const string SectionName = "Security";

    [Range(1, 100)] public int MaxFailedAccessAttempts { get; set; } = 5;
    [Range(1, 1440)] public int LockoutMinutes { get; set; } = 15;
}
