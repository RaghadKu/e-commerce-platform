using ECommerce.Inventory.Contracts;
using ECommerce.SharedKernel.Exceptions;
using Grpc.Core;

namespace ECommerce.Modules.Products.Infrastructure;

internal interface IInventoryClient
{
    Task<int> GetAvailableAsync(Guid productId, CancellationToken ct);
}

internal sealed class InventoryClient(InventoryService.InventoryServiceClient client) : IInventoryClient
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(3);

    public async Task<int> GetAvailableAsync(Guid productId, CancellationToken ct)
    {
        try
        {
            var response = await client.CheckAvailabilityAsync(
                new CheckAvailabilityRequest { ProductIds = { productId.ToString() } },
                deadline: DateTime.UtcNow.Add(Timeout),
                cancellationToken: ct);

            return response.Levels.Single().Available;
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.DeadlineExceeded)
        {
            throw new GatewayTimeoutException("The inventory service took too long to respond. Please try again.");
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable)
        {
            throw new ServiceUnavailableException("The inventory service is currently unavailable. Please try again later.");
        }
    }
}