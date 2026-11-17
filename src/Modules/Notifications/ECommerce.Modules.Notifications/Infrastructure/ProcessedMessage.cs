namespace ECommerce.Modules.Notifications.Infrastructure;

internal sealed class ProcessedMessage
{
    public Guid MessageId { get; set; }
    public string Consumer { get; set; } = string.Empty;
    public DateTime ProcessedAtUtc { get; set; }
}
