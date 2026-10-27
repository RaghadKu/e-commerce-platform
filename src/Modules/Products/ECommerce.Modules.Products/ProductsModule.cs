using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerce.Modules.Products;

public static class ProductsModule
{
    public static IServiceCollection AddProductsModule(
        this IServiceCollection services, IConfiguration configuration)
    {
        // Services will be registered here.
        return services;
    }
}
