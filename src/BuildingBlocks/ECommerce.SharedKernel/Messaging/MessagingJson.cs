using System.Text.Json;

namespace ECommerce.SharedKernel.Messaging;

public static class MessagingJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}
