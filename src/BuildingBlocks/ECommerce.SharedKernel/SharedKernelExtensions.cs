using ECommerce.SharedKernel.Api;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerce.SharedKernel;

public static class SharedKernelExtensions
{
    public static IServiceCollection AddSharedKernel(this IServiceCollection services)
    {
        services.AddProblemDetails(options =>
            options.CustomizeProblemDetails = context =>
            {
                context.ProblemDetails.Instance ??= context.HttpContext.Request.Path.Value;
                context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
            });

        services.AddExceptionHandler<GlobalExceptionHandler>();
        return services;
    }
}
