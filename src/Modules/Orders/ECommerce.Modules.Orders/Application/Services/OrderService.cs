using ECommerce.Inventory.Contracts;
using ECommerce.Inventory.Contracts.PublicApi;
using ECommerce.Modules.Orders.Domain;
using ECommerce.Modules.Orders.Infrastructure;
using ECommerce.Modules.Products.PublicApi;
using ECommerce.SharedKernel.Exceptions;
using ECommerce.SharedKernel.Pagination;
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
    private readonly Guid UserId = httpContextAccessor!.HttpContext!.User.GetUserId();
    private readonly bool isAdmin = httpContextAccessor!.HttpContext!.User.IsAdmin();
   
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


public async Task<PagedResult<OrderDto>> GetAllAsync(
    OrderQuery input,
    CancellationToken cancellationToken)
{
    var page = Math.Max(1, input.Page);
    var pageSize = Math.Clamp(input.PageSize, 1, 100);

    // Route read operations to the replica when appropriate.
    OrdersDbContext source =
        await router.UseReplicaAsync(cancellationToken)
            ? readDb
            : db;

    // Build the query.
    var query = source.Orders.AsNoTracking();

    // Filtering
    if (input.UserId.HasValue)
    {
        query = query.Where(o => o.UserId == input.UserId.Value);
    }

    if (!string.IsNullOrWhiteSpace(input.Status))
    {
        if (!Enum.TryParse<OrderStatus>(
                input.Status, true, out var statusEnum))
        {
            throw new ArgumentException("Invalid order status.");
        }

        query = query.Where(o => o.Status == statusEnum);
    }

    // Total count before pagination.
    var total = await query.CountAsync(cancellationToken);

    // Pagination
    var ordersPage = await query
        .OrderByDescending(o => o.UpdatedAtUtc)
        .ThenByDescending(o => o.CreatedAtUtc)
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .ToListAsync(cancellationToken);

    return new PagedResult<OrderDto>(
        ordersPage.Select(OrderDto.Create).ToList(),
        page,
        pageSize,
        total);
}


    public async Task<OrderDto> GetByIdAsync(Guid Id, CancellationToken ct)
    {
        OrdersDbContext source = await router.UseReplicaAsync(ct) ? readDb : db;

        var order = await source.Orders.AsNoTracking()
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == Id && (o.UserId == UserId || isAdmin), ct);

        if (order is null)
        {
            throw new NotFoundException($"Order with id {Id} not found");
        }

        return OrderDto.Create(order);
    }

    // Handling stock reservation
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

        await inventoryServiceClient.ReserveStockAsync(order.Id, lines, cancellationToken);

        order.Status = OrderStatus.Pending;
        order.UpdatedAtUtc = DateTime.UtcNow;
        db.Update(order);
        await db.SaveChangesAsync(cancellationToken);

        return OrderDto.Create(order);
    }

    // Handling payment and stock adjustment
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

        // reduce stock for each item in the order
        // release reserved stock 

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
            .FirstOrDefaultAsync(o => (o.UserId == UserId || isAdmin) && o.Status == OrderStatus.Pending, cancellationToken);

        if (order is null)
        {
            throw new NotFoundException($"No pending Order to cancel");
        }

        await inventoryServiceClient.ReleaseStockAsync(order.Id, cancellationToken);

        order.Status = OrderStatus.Cancelled;
        order.UpdatedAtUtc = DateTime.UtcNow;
        db.Update(order);
        await db.SaveChangesAsync(cancellationToken);

        return OrderDto.Create(order);
    }

    #endregion
}
