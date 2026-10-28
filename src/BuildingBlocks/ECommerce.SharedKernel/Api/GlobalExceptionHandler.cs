using ECommerce.SharedKernel.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace ECommerce.SharedKernel.Api;

internal sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ProblemDetails problem;

        if (exception is AppException appException)
        {
            logger.LogWarning("Request failed: {ExceptionType} - {Message}",
                exception.GetType().Name, exception.Message);

            problem = appException is ValidationFailedException validation
                ? new ValidationProblemDetails(validation.Errors)
                {
                    Status = appException.StatusCode,
                    Title = appException.Title,
                    Detail = appException.Message
                }
                : new ProblemDetails
                {
                    Status = appException.StatusCode,
                    Title = appException.Title,
                    Detail = appException.Message
                };
        }
        else
        {
            logger.LogError(exception, "Unhandled exception. TraceId: {TraceId}", httpContext.TraceIdentifier);

            problem = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "An unexpected error occurred.",
                Detail = "Please try again later. If the problem persists, report the trace id."
            };
        }

        httpContext.Response.StatusCode = problem.Status!.Value;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception
        });
    }
}
