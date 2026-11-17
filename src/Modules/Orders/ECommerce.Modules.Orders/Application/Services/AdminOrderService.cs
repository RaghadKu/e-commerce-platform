using ECommerce.Inventory.Contracts;
using ECommerce.Modules.Orders.Domain;
using ECommerce.Modules.Orders.Infrastructure;
using ECommerce.Modules.Orders.Infrastructure.Outbox;
using ECommerce.SharedKernel.Exceptions;
using ECommerce.SharedKernel.Messaging;
using ECommerce.SharedKernel.Messaging.Events;
using ECommerce.SharedKernel.Pagination;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Modules.Orders.Application.Services;

internal sealed class AdminOrderService(
    OrdersDbContext db,
    InventoryService.InventoryServiceClient inventory) : IAdminOrderService
{
    private static readonly TimeSpan InventoryTimeout = TimeSpan.FromSeconds(3);

    private static readonly Dictionary<OrderStatus, OrderStatus[]> AllowedTransitions = new()
    {
        [OrderStatus.Pending] = [OrderStatus.Confirmed, OrderStatus.Cancelled],
        [OrderStatus.Confirmed] = [OrderStatus.Processing, OrderStatus.Cancelled],
        [OrderStatus.Processing] = [OrderStatus.Shipped],
        [OrderStatus.Shipped] = [OrderStatus.Delivered]
    };

    public async Task<PagedResult<OrderDto>> GetAllAsync(OrderQuery query, CancellationToken ct)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var orders = db.Orders.AsNoTracking().Where(o => o.Status != OrderStatus.Draft);

        if (!string.IsNullOrWhiteSpace(query.Status)
            && Enum.TryParse<OrderStatus>(query.Status, ignoreCase: true, out var status))
        {
            orders = orders.Where(o => o.Status == status);
        }

        if (query.UserId is { } userId)
            orders = orders.Where(o => o.UserId == userId);

        var total = await orders.CountAsync(ct);
        var items = await orders
            .Include(o => o.Items)
            .OrderByDescending(o => o.UpdatedAtUtc).ThenBy(o => o.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<OrderDto>(items.Select(OrderDto.Create).ToList(), page, pageSize, total);
    }

    public async Task<OrderDto> ChangeStatusAsync(Guid orderId, UpdateOrderStatusRequest request, CancellationToken ct)
    {
        if (!Enum.TryParse<OrderStatus>(request.Status, ignoreCase: true, out var newStatus)
            || !Enum.IsDefined(newStatus))
        {
            throw new ValidationFailedException(new Dictionary<string, string[]>
            {
                ["Status"] = [$"Unknown status. Allowed values: {string.Join(", ", Enum.GetNames<OrderStatus>())}."]
            });
        }

        var order = await db.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId, ct)
            ?? throw new NotFoundException($"Order with id {orderId} not found");

        var oldStatus = order.Status;

        if (!AllowedTransitions.TryGetValue(oldStatus, out var allowed) || !allowed.Contains(newStatus))
        {
            throw new ConflictException($"An order in status {oldStatus} cannot change to {newStatus}.");
        }

        if (newStatus == OrderStatus.Cancelled)
            await ReleaseStockAsync(order.Id, ct);

        order.Status = newStatus;
        order.UpdatedAtUtc = DateTime.UtcNow;

        db.OutboxMessages.Add(OutboxMessage.Create(
            MessagingTopology.OrderStatusChangedRoutingKey,
            new OrderStatusChangedEvent(
                order.Id, order.UserId, oldStatus.ToString(), newStatus.ToString(), order.UpdatedAtUtc)));

        await db.SaveChangesAsync(ct);

        return OrderDto.Create(order);
    }

    private async Task ReleaseStockAsync(Guid orderId, CancellationToken ct)
    {
        ReleaseStockResponse response;

        try
        {
            response = await inventory.ReleaseStockAsync(
                new ReleaseStockRequest { OrderId = orderId.ToString() },
                deadline: DateTime.UtcNow.Add(InventoryTimeout),
                cancellationToken: ct).ResponseAsync;
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.DeadlineExceeded)
        {
            throw new GatewayTimeoutException("The inventory service took too long to respond. Please try again.");
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable)
        {
            throw new ServiceUnavailableException("The inventory service is currently unavailable. Please try again later.");
        }

        switch (response.Status)
        {
            case ReleaseStatus.Released:
            case ReleaseStatus.AlreadyReleased:
            case ReleaseStatus.NotFound:
                break;
            default:
                throw new ServiceUnavailableException("The inventory service could not release the reserved stock.");
        }
    }
}