using System.Globalization;
using System.Net;
using System.Text;
using ECommerce.SharedKernel.Messaging.Events;

namespace ECommerce.Modules.Notifications.Email;

internal static class OrderConfirmationEmail
{
    public static (string Subject, string Html) Build(OrderCreatedEvent e)
    {
        var name = string.IsNullOrWhiteSpace(e.CustomerName) ? "customer" : e.CustomerName;
        var shortId = e.OrderId.ToString("N")[..8].ToUpperInvariant();

        var rows = new StringBuilder();
        foreach (var item in e.Items)
        {
            rows.Append("<tr>")
                .Append($"<td>{WebUtility.HtmlEncode(item.ProductName)}</td>")
                .Append($"<td style=\"text-align:right\">{item.Quantity}</td>")
                .Append($"<td style=\"text-align:right\">{item.UnitPrice.ToString("N2", CultureInfo.InvariantCulture)}</td>")
                .Append("</tr>");
        }

        var html = $"""
            <h2>Thank you for your order, {WebUtility.HtmlEncode(name)}!</h2>
            <p>We received order <b>#{shortId}</b>. It is waiting for confirmation.</p>
            <table border="1" cellpadding="6" cellspacing="0">
              <tr><th>Product</th><th>Qty</th><th>Unit price</th></tr>
              {rows}
            </table>
            <p><b>Total: {e.TotalAmount.ToString("N2", CultureInfo.InvariantCulture)}</b></p>
            """;

        return ($"Order #{shortId} received", html);
    }
}