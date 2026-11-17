namespace ECommerce.Modules.Notifications.Email;

internal interface IEmailSender
{
    Task SendAsync(string toEmail, string toName, string subject, string htmlBody, CancellationToken ct);
}
