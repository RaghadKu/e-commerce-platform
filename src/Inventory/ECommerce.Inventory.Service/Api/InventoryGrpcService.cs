using ECommerce.Inventory.Contracts;
using ECommerce.Inventory.Service.Application;
using Grpc.Core;

namespace ECommerce.Inventory.Service.Api;

internal sealed class InventoryGrpcService(StockManager stock) : InventoryService.InventoryServiceBase
{
    private const int MaxItems = 100;
    private const int MaxQuantity = 100_000;

    public override async Task<CheckAvailabilityResponse> CheckAvailability(
        CheckAvailabilityRequest request, ServerCallContext context)
    {
        if (request.ProductIds.Count is 0 or > MaxItems)
            throw Invalid($"product_ids must contain 1 to {MaxItems} ids.");

        var ids = request.ProductIds.Select(id => ParseId(id, "product_id")).Distinct().ToList();

        var response = new CheckAvailabilityResponse();
        response.Levels.AddRange((await stock.CheckAsync(ids, context.CancellationToken)).Select(ToMessage));

        return response;
    }

    public override async Task<ReserveStockResponse> ReserveStock(ReserveStockRequest request, ServerCallContext context)
    {
        var orderId = ParseId(request.OrderId, "order_id");

        if (request.Lines.Count is 0 or > MaxItems)
            throw Invalid($"lines must contain 1 to {MaxItems} items.");

        var lines = request.Lines.Select(l =>
        {
            if (l.Quantity is < 1 or > MaxQuantity)
                throw Invalid($"quantity must be between 1 and {MaxQuantity}.");
            return (ProductId: ParseId(l.ProductId, "product_id"), l.Quantity);
        }).ToList();

        var result = await stock.ReserveAsync(orderId, lines, context.CancellationToken);

        var response = new ReserveStockResponse { Status = result.Status };

        response.Shortages.AddRange(result.Shortages.Select(s => new StockShortage
        {
            ProductId = s.ProductId.ToString(),
            Requested = s.Requested,
            Available = s.Available
        }));

        return response;
    }

    public override async Task<ReleaseStockResponse> ReleaseStock(ReleaseStockRequest request, ServerCallContext context)
    {
        return new() { Status = await stock.ReleaseAsync(ParseId(request.OrderId, "order_id"), context.CancellationToken) };
    }

    public override async Task<SetStockResponse> SetStock(SetStockRequest request, ServerCallContext context)
    {
        var productId = ParseId(request.ProductId, "product_id");

        if (request.QuantityOnHand is < 0 or > MaxQuantity)
            throw Invalid($"quantity_on_hand must be between 0 and {MaxQuantity}.");

        if (!await stock.SetStockAsync(productId, request.QuantityOnHand, context.CancellationToken))
            throw new RpcException(new Status(StatusCode.FailedPrecondition,
                "The quantity cannot be lower than the currently reserved quantity."));

        var level = (await stock.CheckAsync([productId], context.CancellationToken)).Single();

        return new SetStockResponse { Level = ToMessage(level) };
    }

    private static StockLevel ToMessage(StockSnapshot s) => new()
    {
        ProductId = s.ProductId.ToString(),
        OnHand = s.OnHand,
        Reserved = s.Reserved,
        Available = s.OnHand - s.Reserved
    };

    private static Guid ParseId(string value, string field) =>
        Guid.TryParse(value, out var id) && id != Guid.Empty ? id : throw Invalid($"{field} is not a valid id.");

    private static RpcException Invalid(string message) => new(new Status(StatusCode.InvalidArgument, message));
}
