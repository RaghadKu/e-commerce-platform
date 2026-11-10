using ECommerce.Inventory.Contracts.PublicApi;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerce.Inventory.Contracts;

public static class InventoryContractsExtensions
{
    public static IServiceCollection AddInventoryContracts(this IServiceCollection services)
    {
        // Implementation for adding inventory contracts
        services.AddScoped<IInventoryServiceClient, InventoryServiceClient>();
        return services;
    }
}
