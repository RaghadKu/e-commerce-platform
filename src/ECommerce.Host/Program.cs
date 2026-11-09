using Asp.Versioning.ApiExplorer;
using ECommerce.Host.Configuration;

using ECommerce.Host.Seeders;
using ECommerce.Host.Middlewares;
using ECommerce.Modules.Notifications;
using ECommerce.Modules.Orders;
using ECommerce.Modules.Products;
using ECommerce.Modules.Users;
using ECommerce.SharedKernel;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// 1. Register Controllers 
// Note: If your module controllers live in separate class libraries (e.g., Users.Api), 
// make sure to add application parts here if they aren't auto-discovered:
builder.Services.AddControllers();
// .AddApplicationPart(typeof(ECommerce.Modules.Users.Presentation.SomeUserController).Assembly);

builder.Services.AddEndpointsApiExplorer();

// 2. Register SharedKernel services FIRST (this registers AddApiVersioning & AddApiExplorer)
builder.Services.AddSharedKernel(builder.Configuration);

// 3. Register Module services
builder.Services.AddUsersModule(builder.Configuration);
builder.Services.AddProductsModule(builder.Configuration);
builder.Services.AddOrdersModule(builder.Configuration);
builder.Services.AddNotificationsModule(builder.Configuration);

builder.Services.AddHealthChecks();

// 4. Register the Swagger options configuration
builder.Services.ConfigureOptions<ConfigureSwaggerOptions>();

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "E-Commerce Platform API",
        Version = "v1"
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste the access token only (without the word 'Bearer')."
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddHealthChecks();
builder.Services.AddSharedKernel(builder.Configuration);  

builder.Services.AddUsersModule(builder.Configuration);
builder.Services.AddProductsModule(builder.Configuration);
builder.Services.AddOrdersModule(builder.Configuration);
builder.Services.AddNotificationsModule(builder.Configuration);

builder.Services.AddScoped<DevelopmentDataSeeder>();

var app = builder.Build();

if (builder.Configuration.GetValue<bool>("seed-catalog"))
{
    if (!app.Environment.IsDevelopment())
    {
        throw new InvalidOperationException(
            "Database seeding is only allowed in Development.");
    }

    await using var scope = app.Services.CreateAsyncScope();

    var seeder = scope.ServiceProvider
        .GetRequiredService<DevelopmentDataSeeder>();

    await seeder.SeedAsync(CancellationToken.None);

    return;
}

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        // Safe to resolve from app.Services because the container is already built
        var provider = app.Services.GetRequiredService<IApiVersionDescriptionProvider>();

        // Generate an endpoint dropdown entry in Swagger UI for each API version
        foreach (var description in provider.ApiVersionDescriptions)
        {
            options.SwaggerEndpoint(
                $"/swagger/{description.GroupName}/swagger.json",
                description.GroupName.ToUpperInvariant()
            );
        }
    });
}

app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<RequestTimingMiddleware>();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

public partial class Program;