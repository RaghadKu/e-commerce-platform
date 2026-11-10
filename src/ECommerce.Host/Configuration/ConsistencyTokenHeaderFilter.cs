using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace ECommerce.Host.Configuration;

public sealed class ConsistencyTokenHeaderFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        operation.Parameters ??= new List<OpenApiParameter>();
        operation.Parameters.Add(new OpenApiParameter
        {
            Name = "X-Consistency-Token",
            In = ParameterLocation.Header,
            Required = false,
            Description = "Token from the last write response. Leave empty to simulate a client that does not send it.",
            Schema = new OpenApiSchema { Type = "integer", Format = "int64" }
        });
    }
}