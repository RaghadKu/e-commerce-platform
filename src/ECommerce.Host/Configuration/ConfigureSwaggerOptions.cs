namespace ECommerce.Host.Configuration;

using Asp.Versioning.ApiExplorer;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

public class ConfigureSwaggerOptions : IConfigureOptions<SwaggerGenOptions>
{
    private readonly IApiVersionDescriptionProvider _provider;

    public ConfigureSwaggerOptions(IApiVersionDescriptionProvider provider)
    {
        _provider = provider;
    }

    public void Configure(SwaggerGenOptions options)
    {
        // Dynamically add a Swagger document for each discovered API version
        foreach (var description in _provider.ApiVersionDescriptions)
        {
            options.SwaggerDoc(
                description.GroupName,
                new OpenApiInfo
                {
                    Title = "E-Commerce Platform API",
                    Version = description.ApiVersion.ToString()
                });
        }
    }
}