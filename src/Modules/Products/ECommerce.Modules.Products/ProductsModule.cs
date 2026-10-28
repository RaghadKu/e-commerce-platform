using ECommerce.Modules.Products.Application;
using ECommerce.Modules.Products.Application.Services;
using ECommerce.Modules.Products.Infrastructure;
using ECommerce.Modules.Products.PublicApi;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerce.Modules.Products;

public static class ProductsModule
{
    public static IServiceCollection AddProductsModule(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ProductImageOptions>()
            .Bind(configuration.GetSection(ProductImageOptions.SectionName))
            .ValidateDataAnnotations().ValidateOnStart();

        services.AddDbContext<ProductsDbContext>((sp, options) =>
        {
            var connectionString = sp.GetRequiredService<IConfiguration>().GetConnectionString("MainDb")
                ?? throw new InvalidOperationException("Connection string 'MainDb' is not configured.");

            options.UseSqlServer(connectionString, sql 
                => sql.MigrationsHistoryTable("__EFMigrationsHistory", ProductsDbContext.Schema));
        });

        services.AddSingleton<IImageStorage, LocalImageStorage>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IProductImageService, ProductImageService>();
        services.AddScoped<IProductCatalog, ProductCatalog>();
        services.AddHostedService<ProductsDbInitializer>();

        return services;
    }
}
