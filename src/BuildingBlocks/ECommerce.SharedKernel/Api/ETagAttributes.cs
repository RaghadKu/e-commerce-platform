using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Net.Http.Headers;

namespace ECommerce.SharedKernel.Api;

[AttributeUsage(AttributeTargets.Method)]
public sealed class ETagAttribute : Attribute, IAsyncResultFilter
{
    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        var request = context.HttpContext.Request;
        var response = context.HttpContext.Response;

        if (!HttpMethods.IsGet(request.Method)
            || context.Result is not ObjectResult { StatusCode: null or 200, Value: { } value })
        {
            await next();
            return;
        }

        var hash = SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value, value.GetType()));
        var etag = new EntityTagHeaderValue($"\"{Convert.ToHexString(hash)}\"");

        response.GetTypedHeaders().ETag = etag;
        response.Headers.CacheControl = "no-cache";

        if (request.GetTypedHeaders().IfNoneMatch.Any(t => t.Compare(etag, useStrongComparison: false)))
            context.Result = new StatusCodeResult(StatusCodes.Status304NotModified);

        await next();
    }
}