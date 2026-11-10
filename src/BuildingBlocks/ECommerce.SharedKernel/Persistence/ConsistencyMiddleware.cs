using Microsoft.AspNetCore.Http;

namespace ECommerce.SharedKernel.Persistence;

public sealed class ConsistencyMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext http, ConsistencyContext consistency)
    {
        if (http.Request.Headers.TryGetValue(ConsistencyContext.HeaderName, out var raw)
            && long.TryParse(raw.ToString(), out var token) && token >= 0)
        {
            consistency.RequiredToken = token;
        }

        http.Response.OnStarting(() =>
        {
            if (consistency.WriteToken is { } written)
                http.Response.Headers[ConsistencyContext.HeaderName] = written.ToString();
            return Task.CompletedTask;
        });

        return next(http);
    }
}