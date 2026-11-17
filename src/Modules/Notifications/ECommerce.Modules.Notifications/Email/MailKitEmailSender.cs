using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace ECommerce.Modules.Notifications.Email;

internal sealed class MailKitEmailSender(
    IOptions<EmailOptions> options,
    ILogger<MailKitEmailSender> logger) : IEmailSender
{
    public async Task SendAsync(
        string toEmail, string toName, string subject, string htmlBody, CancellationToken ct)
    {
        var o = options.Value;

        if (o.SimulateFailure)
            throw new InvalidOperationException("Simulated email failure (Email:SimulateFailure is true).");

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(o.FromName, o.FromAddress));
        message.To.Add(new MailboxAddress(toName, toEmail));
        message.Subject = subject;
        message.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

        using var client = new SmtpClient();
        await client.ConnectAsync(o.Host, o.Port, SecureSocketOptions.StartTlsWhenAvailable, ct);
        await client.AuthenticateAsync(o.UserName, o.Password, ct);
        await client.SendAsync(message, ct);
        await client.DisconnectAsync(true, ct);

        logger.LogInformation("Email sent to {Email}.", toEmail);
    }
}
