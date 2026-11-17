using ECommerce.Modules.Notifications.Hubs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace ECommerce.Modules.Notifications;

public static class NotificationsEndpoints
{
    public static IEndpointRouteBuilder MapNotificationsHubs(this IEndpointRouteBuilder app)
    {
        app.MapHub<OrdersHub>("/hubs/orders", o => o.CloseOnAuthenticationExpiration = true);
        app.MapHub<AdminHub>("/hubs/admin", o => o.CloseOnAuthenticationExpiration = true);
        return app;
    }
}