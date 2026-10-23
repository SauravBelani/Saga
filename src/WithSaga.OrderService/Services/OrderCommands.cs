using Microsoft.EntityFrameworkCore;
using WithSaga.OrderService.Contracts;
using WithSaga.OrderService.Data;
using WithSaga.OrderService.Models;

namespace WithSaga.OrderService.Services;

/// <summary>
/// Local order changes. This service does not call Inventory or Payment.
/// </summary>
public class OrderCommands
{
    private readonly OrderDbContext _db;

    public OrderCommands(OrderDbContext db)
    {
        _db = db;
    }

    public async Task<OrderCommandResult> CreateAsync(CreateOrderRequest request, CancellationToken cancellationToken)
    {
        var order = new Order
        {
            ProductId = request.ProductId.Trim(),
            Quantity = request.Quantity,
            Amount = request.Amount,
            Status = OrderStatus.Created,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.Orders.Add(order);
        await _db.SaveChangesAsync(cancellationToken);

        return OrderCommandResult.Ok(new OrderResponse
        {
            Message = $"Order {order.Id} is Created and committed in the Order database.",
            OrderId = order.Id,
            Status = order.Status.ToString()
        });
    }

    public Task<OrderCommandResult> ConfirmAsync(int orderId, CancellationToken cancellationToken)
    {
        return SetStatusAsync(orderId, OrderStatus.Confirmed, cancellationToken);
    }

    public Task<OrderCommandResult> CancelAsync(int orderId, CancellationToken cancellationToken)
    {
        return SetStatusAsync(orderId, OrderStatus.Cancelled, cancellationToken);
    }

    private async Task<OrderCommandResult> SetStatusAsync(int orderId, OrderStatus status, CancellationToken cancellationToken)
    {
        var order = await _db.Orders.FirstOrDefaultAsync(row => row.Id == orderId, cancellationToken);
        if (order is null)
        {
            return OrderCommandResult.Fail(StatusCodes.Status404NotFound, $"Order {orderId} was not found.");
        }

        order.Status = status;
        await _db.SaveChangesAsync(cancellationToken);

        return OrderCommandResult.Ok(new OrderResponse
        {
            Message = $"Order {order.Id} is {order.Status} and committed in the Order database.",
            OrderId = order.Id,
            Status = order.Status.ToString()
        });
    }
}

public sealed class OrderCommandResult
{
    public int StatusCode { get; private init; }

    public string? ErrorMessage { get; private init; }

    public OrderResponse? Body { get; private init; }

    public static OrderCommandResult Ok(OrderResponse body) => new()
    {
        StatusCode = StatusCodes.Status200OK,
        Body = body
    };

    public static OrderCommandResult Fail(int statusCode, string message) => new()
    {
        StatusCode = statusCode,
        ErrorMessage = message
    };
}
