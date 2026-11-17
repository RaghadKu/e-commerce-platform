using System.ComponentModel.DataAnnotations;

namespace ECommerce.Modules.Notifications.Email;

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    [Required] public string Host { get; set; } = string.Empty;
    [Range(1, 65535)] public int Port { get; set; } = 2525;
    [Required] public string UserName { get; set; } = string.Empty;
    [Required] public string Password { get; set; } = string.Empty;
    [Required] public string FromAddress { get; set; } = "shop@ecommerce.local";
    [Required] public string FromName { get; set; } = "E-Commerce Shop";

    public bool SimulateFailure { get; set; }
}
