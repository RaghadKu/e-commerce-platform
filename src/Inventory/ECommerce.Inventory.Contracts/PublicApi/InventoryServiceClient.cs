using ECommerce.SharedKernel.Exceptions;
using Grpc.Core;

namespace ECommerce.Inventory.Contracts.PublicApi;

public class InventoryServiceClient(InventoryService.InventoryServiceClient client) : IInventoryServiceClient
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(3);
    private const int MaxReservationLines = 100;
    private const int MaxLineQuantity = 100_000;

    public Task<int> GetAvailableAsync(Guid productId, CancellationToken ct)
        => CallAsync(async (ct) =>
        {
            var response = await client.CheckAvailabilityAsync(
                new CheckAvailabilityRequest { ProductIds = { productId.ToString() } },
                deadline: DateTime.UtcNow.Add(Timeout),
                cancellationToken: ct);

            return response.Levels.Single().Available;
        }, ct);

    public async Task<StockLevelDto> SetStockAsync(Guid productId, int quantityOnHand, CancellationToken ct)
    {
        try
        {
            return await CallAsync(async (ct) =>
            {
                var response = await client.SetStockAsync(
                    new SetStockRequest { ProductId = productId.ToString(), QuantityOnHand = quantityOnHand },
                    deadline: DateTime.UtcNow.Add(Timeout),
                    cancellationToken: ct);

                var level = response.Level;
                return new StockLevelDto(productId, level.OnHand, level.Reserved, level.Available);
            }, ct);
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

    public async Task ReserveStockAsync(Guid orderId, List<(Guid ProductId, int Quantity)> lines, CancellationToken ct)
    {
        if (lines.Count is 0 or > MaxReservationLines || lines.Any(l => l.Quantity is < 1 or > MaxLineQuantity))
        {
            throw new ValidationFailedException(new Dictionary<string, string[]>
            {
                ["Items"] = [$"The order must contain 1 to {MaxReservationLines} products, each with a quantity from 1 to {MaxLineQuantity}."]
            });
        }

        var reserveResult = await CallAsync(
            ct => client.ReserveStockAsync(
                new ReserveStockRequest
                {
                    OrderId = orderId.ToString(),
                    Lines =
                    {
                        lines.Select(l => new StockQuantity
                        {
                            ProductId = l.ProductId.ToString(),
                            Quantity = l.Quantity
                        })
                    }
                },
                deadline: DateTime.UtcNow.Add(Timeout),
                cancellationToken: ct).ResponseAsync,
            ct);

        switch (reserveResult.Status)
        {
            case ReserveStatus.Reserved:
                break;
            case ReserveStatus.InsufficientStock:
                throw new ConflictException(DescribeShortages(reserveResult));
            case ReserveStatus.AlreadyReleased:
                throw new ConflictException("Stock for this order was already released and cannot be reserved again.");
            default:
                throw new ServiceUnavailableException("The inventory service returned an unexpected reservation status.");
        }
    }

    public async Task ReleaseStockAsync(Guid orderId, CancellationToken ct)
    {
        var releaseResult = await CallAsync(
            ct => client.ReleaseStockAsync(
                new ReleaseStockRequest { OrderId = orderId.ToString() },
                deadline: DateTime.UtcNow.Add(Timeout),
                cancellationToken: ct).ResponseAsync,
            ct);

        switch (releaseResult.Status)
        {
            case ReleaseStatus.Released:
            case ReleaseStatus.AlreadyReleased:
            case ReleaseStatus.NotFound:
                break;
            default:
                throw new ServiceUnavailableException("The inventory service could not release the reserved stock.");
        }
    }

    // Helper methods

    private static async Task<T> CallAsync<T>(Func<CancellationToken, Task<T>> call, CancellationToken ct)
    {
        try
        {
            return await call(ct);
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

    private static string DescribeShortages(ReserveStockResponse response)
    {
        if (response.Shortages.Count == 0)
            return "One or more items do not have enough stock.";

        var details = string.Join("; ", response.Shortages.Select(s =>
            $"product {s.ProductId} requested {s.Requested} but only {s.Available} available"));

        return $"Insufficient stock: {details}.";
    }
}
