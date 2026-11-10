using Asp.Versioning.ApiExplorer;
using ECommerce.Host.Configuration;
using ECommerce.Host.Middlewares;
using ECommerce.Host.Seeders;
using ECommerce.Inventory.Contracts;
using ECommerce.Modules.Notifications;
using ECommerce.Modules.Orders;
using ECommerce.Modules.Products;
using ECommerce.Modules.Users;
using ECommerce.SharedKernel;
using ECommerce.SharedKernel.Persistence;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Controllers
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Shared Kernel
builder.Services.AddSharedKernel(builder.Configuration);
builder.Services.AddReadConsistency();

// Modules
builder.Services.AddUsersModule(builder.Configuration);
builder.Services.AddProductsModule(builder.Configuration);
builder.Services.AddOrdersModule(builder.Configuration);
builder.Services.AddNotificationsModule(builder.Configuration);
builder.Services.AddInventoryContracts();

// Health Checks
builder.Services.AddHealthChecks();

// Swagger with API Versioning
builder.Services.ConfigureOptions<ConfigureSwaggerOptions>();

builder.Services.AddSwaggerGen(options =>
{
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

// Seeder
builder.Services.AddScoped<DevelopmentDataSeeder>();

var app = builder.Build();

// Database Seeding
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

// Exception Handling
app.UseExceptionHandler();
app.UseStatusCodePages();

// Swagger
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();

    app.UseSwaggerUI(options =>
    {
        var provider = app.Services
            .GetRequiredService<IApiVersionDescriptionProvider>();

        foreach (var description in provider.ApiVersionDescriptions)
        {
            options.SwaggerEndpoint(
                $"/swagger/{description.GroupName}/swagger.json",
                description.GroupName.ToUpperInvariant());
        }
    });
}

// Authentication & Authorization
app.UseAuthentication();
app.UseAuthorization();

// Custom Middleware
app.UseMiddleware<RequestTimingMiddleware>();
app.UseMiddleware<ConsistencyMiddleware>();

// Endpoints
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

public partial class Program;
