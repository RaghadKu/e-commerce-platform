namespace ECommerce.Host.Middlewares;

using System.Diagnostics;

public class RequestTimingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestTimingMiddleware> _logger;

    public RequestTimingMiddleware(RequestDelegate next, ILogger<RequestTimingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        long startTime = Stopwatch.GetTimestamp();

        try
        {
            await _next(context);
        }
        finally
        {
            TimeSpan elapsed = Stopwatch.GetElapsedTime(startTime);

            _logger.LogInformation("[Perf] HTTP {Method} {Path} finished in {ElapsedMs:F2}ms with status {StatusCode}",
                context.Request.Method,
                context.Request.Path,
                elapsed.TotalMilliseconds,
                context.Response.StatusCode);
        }
    }
}
