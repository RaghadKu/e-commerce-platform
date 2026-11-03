using Azure.Core;
using ECommerce.Modules.Orders.Domain;
using ECommerce.Modules.Orders.Infrastructure;
using ECommerce.Modules.Products.PublicApi;
using ECommerce.SharedKernel.Exceptions;
using ECommerce.SharedKernel.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.Net.NetworkInformation;

namespace ECommerce.Modules.Orders.Application.Services;

internal class OrderService(OrdersDbContext db, IProductCatalog catalog, IHttpContextAccessor httpContextAccessor) : IOrderService
{
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
        var order = await db.Orders.FirstOrDefaultAsync(o => o.UserId == UserId && o.Status == OrderStatus.Draft);
        
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

        var orderItem = new OrderItem
        {
           ProductId = request.ProductId,
           Price = productsList[0].Price,
           Qty = request.Qty
        };

        order.Items.Add(orderItem);

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
        var ordersList = await db.Orders.ToListAsync(cancellationToken);

        var response = ordersList.Select(o => OrderDto.Create(o)).ToList();

        return response;
    }

    public async Task<OrderDto> GetByIdAsync(Guid Id, CancellationToken ct)
    {
        var order = await db.Orders
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
            .FirstOrDefaultAsync(o => o.UserId == UserId && o.Status == OrderStatus.Draft);

        if (order is null)
        {
            throw new NotFoundException($"No order in draft status is found");
        }

        // Reserve stock for each item in the order using inventory service
        // implement here

        order.Status = OrderStatus.Pending;
        db.Update(order);
        await db.SaveChangesAsync();

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
            .FirstOrDefaultAsync(o => o.UserId == UserId && o.Status == OrderStatus.Pending);

        if (order is null)
        {
            throw new NotFoundException($"No pending Order to cancel");
        }

        // Release stock for each item in the order using inventory service
        // implement here

        order.Status = OrderStatus.Cancelled;
        db.Update(order);
        await db.SaveChangesAsync();

        return OrderDto.Create(order);
    }

    #endregion
}
