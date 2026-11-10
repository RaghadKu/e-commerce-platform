using ECommerce.Inventory.Contracts;
using ECommerce.Modules.Products.Application;
using ECommerce.SharedKernel.Exceptions;
using Grpc.Core;

namespace ECommerce.Modules.Products.Infrastructure;

internal sealed class InventoryClient(InventoryService.InventoryServiceClient client) : IInventoryClient
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(3);

    public Task<StockLevelDto> GetAvailableAsync(Guid productId, CancellationToken ct)
        => CallAsync(async () =>
        {
            var response = await client.CheckAvailabilityAsync(
                new CheckAvailabilityRequest { ProductIds = { productId.ToString() } },
                deadline: DateTime.UtcNow.Add(Timeout),
                cancellationToken: ct);

            var level = response.Levels.Single();
            return new StockLevelDto(productId, level.OnHand, level.Reserved, level.Available);
        });

    public async Task<StockLevelDto> SetStockAsync(Guid productId, int quantityOnHand, CancellationToken ct)
    {
        try
        {
            return await CallAsync(async () =>
            {
                var response = await client.SetStockAsync(
                    new SetStockRequest { ProductId = productId.ToString(), QuantityOnHand = quantityOnHand },
                    deadline: DateTime.UtcNow.Add(Timeout),
                    cancellationToken: ct);

                var level = response.Level;
                return new StockLevelDto(productId, level.OnHand, level.Reserved, level.Available);
            });
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.FailedPrecondition)
        {
            throw new ConflictException(ex.Status.Detail);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.InvalidArgument)
        {
            throw new ValidationFailedException(new Dictionary<string, string[]>
            {
                ["quantityOnHand"] = [ex.Status.Detail]
            });
        }
    }

    private static async Task<T> CallAsync<T>(Func<Task<T>> call)
    {
        try
        {
            return await call();
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