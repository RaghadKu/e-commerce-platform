using ECommerce.Inventory.Contracts;
using ECommerce.Modules.Orders.Domain;
using ECommerce.Modules.Orders.Infrastructure;
using ECommerce.Modules.Products.PublicApi;
using ECommerce.SharedKernel.Exceptions;
using ECommerce.SharedKernel.Persistence;
using ECommerce.SharedKernel.Security;
using Grpc.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Modules.Orders.Application.Services;

internal class OrderService(OrdersDbContext db, OrdersReadDbContext readDb, IReadRouter router,
    IProductCatalog catalog, IHttpContextAccessor httpContextAccessor,
    InventoryService.InventoryServiceClient inventoryServiceClient) : IOrderService
{
    private const int MaxReservationLines = 100;
    private const int MaxLineQuantity = 100_000;
    private static readonly TimeSpan InventoryTimeout = TimeSpan.FromSeconds(3);

    private readonly Guid UserId = httpContextAccessor!.HttpContext!.User.GetUserId();

    // Managing order items
    #region

    public async Task<OrderDto> AddItemAsync(AddOrderItemRequest request, CancellationToken ct)
    {
        if (request.Qty <= 0)
        {
            var errors = new Dictionary<string, string[]>
            {
                { "Qty", new[] { "Quantity must be greater than zero." } },
            };
            throw new ValidationFailedException(errors);
        }

        bool isNewOrder = false;
        var order = await db.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.UserId == UserId && o.Status == OrderStatus.Draft, cancellationToken: ct);

        if (order is null)
        {
            order ??= Order.Create(UserId);
            isNewOrder = true;
        }

        var productsList = await catalog.GetProductsAsync([request.ProductId], ct);

        if (productsList.Count == 0)
        {
            throw new NotFoundException($"Product with id {request.ProductId} not found");
        }

        var oldOrderItem = order.Items.FirstOrDefault(i => i.ProductId == request.ProductId);

        if (oldOrderItem is not null)
        {
            oldOrderItem.Qty += request.Qty;
        }
        else
        {
            var orderItem = new OrderItem
            {
                ProductId = request.ProductId,
                Price = productsList[0].Price,
                Qty = request.Qty
            };
            order.Items.Add(orderItem);
        }

        if (isNewOrder)
            await db.AddAsync(order, ct);
        else
        {
            order.UpdatedAtUtc = DateTime.UtcNow;
            db.Update(order);
        }

        await db.SaveChangesAsync(ct);

        return OrderDto.Create(order);
    }

    public async Task<OrderDto> RemoveItemAsync(Guid orderItemId, CancellationToken ct)
    {
        var order = await db.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.UserId == UserId && o.Status == OrderStatus.Draft, ct);

        var orderItem = order?.Items.FirstOrDefault(e => e.Id == orderItemId);

        if (order == null || orderItem == null)
        {
            throw new NotFoundException($"Order item with id {orderItemId} not found");
        }

        order.Items.Remove(orderItem);
        db.Update(order);
        await db.SaveChangesAsync(ct);

        return OrderDto.Create(order);
    }

    public async Task<OrderDto> UpdateItemAsync(UpdateOrderItemRequest request, CancellationToken ct)
    {
        if (request.Qty <= 0)
        {
            var errors = new Dictionary<string, string[]>
            {
                { "Qty", new[] { "Quantity must be greater than zero." } },
            };
            throw new ValidationFailedException(errors);
        }

        var order = await db.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.UserId == UserId && o.Status == OrderStatus.Draft, ct);

        var orderItem = order?.Items.FirstOrDefault(e => e.Id == request.OrderItemId);

        if (order == null || orderItem == null)
        {
            throw new NotFoundException($"Order item with id {request.OrderItemId} not found");
        }

        orderItem.Qty = request.Qty;

        db.Update(order);
        await db.SaveChangesAsync(ct);

        return OrderDto.Create(order);
    }

    #endregion

    // Managing order
    #region

    // Should update to support pagination
    public async Task<List<OrderDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        OrdersDbContext source = await router.UseReplicaAsync(cancellationToken) ? readDb : db;
        var ordersList = await source.Orders.AsNoTracking().Include(o => o.Items).ToListAsync(cancellationToken);

        var response = ordersList.Select(o => OrderDto.Create(o)).ToList();

        return response;
    }

    public async Task<OrderDto> GetByIdAsync(Guid Id, CancellationToken ct)
    {
        OrdersDbContext source = await router.UseReplicaAsync(ct) ? readDb : db;

        var order = await source.Orders.AsNoTracking()
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == Id && o.UserId == UserId, ct);

        if (order is null)
        {
            throw new NotFoundException($"Order with id {Id} not found");
        }

        return OrderDto.Create(order);
    }

    // Handling inventory updates
    public async Task<OrderDto> CheckoutAsync(CancellationToken cancellationToken)
    {
        var order = await db.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.UserId == UserId && o.Status == OrderStatus.Draft, cancellationToken);

        if (order is null)
        {
            throw new NotFoundException($"No order in draft status is found");
        }

        var lines = order.Items
            .GroupBy(i => i.ProductId)
            .Select(g => (ProductId: g.Key, Quantity: g.Sum(i => i.Qty)))
            .ToList();

        if (lines.Count is 0 or > MaxReservationLines || lines.Any(l => l.Quantity is < 1 or > MaxLineQuantity))
        {
            throw new ValidationFailedException(new Dictionary<string, string[]>
            {
                ["Items"] = [$"The order must contain 1 to {MaxReservationLines} products, each with a quantity from 1 to {MaxLineQuantity}."]
            });
        }

        var reserveResult = await CallInventoryAsync(
            ct => inventoryServiceClient.ReserveStockAsync(
                new ReserveStockRequest
                {
                    OrderId = order.Id.ToString(),
                    Lines =
                    {
                        lines.Select(l => new StockQuantity
                        {
                            ProductId = l.ProductId.ToString(),
                            Quantity = l.Quantity
                        })
                    }
                },
                deadline: DateTime.UtcNow.Add(InventoryTimeout),
                cancellationToken: ct).ResponseAsync,
            cancellationToken);

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

        order.Status = OrderStatus.Pending;
        order.UpdatedAtUtc = DateTime.UtcNow;
        db.Update(order);
        await db.SaveChangesAsync(cancellationToken);

        return OrderDto.Create(order);
    }

    //Handling Payment
    public async Task<OrderDto> ConfirmAsync(CancellationToken cancellationToken)
    {
        var order = await db.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.UserId == UserId && o.Status == OrderStatus.Pending);

        if (order is null)
        {
            throw new NotFoundException($"No pending Order to confirm");
        }

        // pay for order using payment service
        // implement here

        order.Status = OrderStatus.Confirmed;
        db.Update(order);
        await db.SaveChangesAsync();

        return OrderDto.Create(order);
    }

    // Release stock for each item in the order 
    public async Task<OrderDto> CancelAsync(CancellationToken cancellationToken)
    {
        var order = await db.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.UserId == UserId && o.Status == OrderStatus.Pending, cancellationToken);

        if (order is null)
        {
            throw new NotFoundException($"No pending Order to cancel");
        }

        var releaseResult = await CallInventoryAsync(
            ct => inventoryServiceClient.ReleaseStockAsync(
                new ReleaseStockRequest { OrderId = order.Id.ToString() },
                deadline: DateTime.UtcNow.Add(InventoryTimeout),
                cancellationToken: ct).ResponseAsync,
            cancellationToken);

        switch (releaseResult.Status)
        {
            case ReleaseStatus.Released:
            case ReleaseStatus.AlreadyReleased:
            case ReleaseStatus.NotFound:
                break;
            default:
                throw new ServiceUnavailableException("The inventory service could not release the reserved stock.");
        }

        order.Status = OrderStatus.Cancelled;
        order.UpdatedAtUtc = DateTime.UtcNow;
        db.Update(order);
        await db.SaveChangesAsync(cancellationToken);

        return OrderDto.Create(order);
    }

    private async Task<T> CallInventoryAsync<T>(Func<CancellationToken, Task<T>> call, CancellationToken ct)
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

    #endregion
}
