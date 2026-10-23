using WithoutSaga.OrderService.Clients;
using WithoutSaga.OrderService.Contracts;
using WithoutSaga.OrderService.Data;
using WithoutSaga.OrderService.Models;

namespace WithoutSaga.OrderService.Services;

/// <summary>
/// Phase 1 place-order flow, without a Saga.
/// Each step commits in its own database. A later failure leaves the earlier commits in place.
/// </summary>
public class OrderPlacementService
{
    private readonly OrderDbContext _db;
    private readonly InventoryClient _inventory;
    private readonly PaymentClient _payment;
    private readonly BusinessStateReader _state;

    public OrderPlacementService(
        OrderDbContext db,
        InventoryClient inventory,
        PaymentClient payment,
        BusinessStateReader state)
    {
        _db = db;
        _inventory = inventory;
        _payment = payment;
        _state = state;
    }

    public async Task<PlacementResult> PlaceAsync(CreateOrderRequest request, CancellationToken cancellationToken)
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

        // This commit ends here. A later failure in Inventory or Payment cannot undo this row.
        await _db.SaveChangesAsync(cancellationToken);

        var reserve = await _inventory.ReserveAsync(order.Id, order.ProductId, order.Quantity, cancellationToken);
        if (!reserve.Succeeded)
        {
            return await StoppedAsync(
                order.Id,
                "Inventory reserve failed after the order row was committed. The order stays Created.",
                cancellationToken);
        }

        var payment = await _payment.ChargeAsync(order.Id, order.Amount, cancellationToken);
        if (!payment.Succeeded)
        {
            // The order row and the reservation are already committed. This phase does not release stock or cancel the order.
            return await StoppedAsync(
                order.Id,
                "Payment failed. The order row and the stock reservation stay committed in their own databases.",
                cancellationToken);
        }

        order.Status = OrderStatus.Confirmed;
        await _db.SaveChangesAsync(cancellationToken);

        return new PlacementResult(
            StatusCodes.Status200OK,
            new PlaceOrderResponse
            {
                Completed = true,
                Outcome = PlaceOrderOutcomes.Completed,
                Summary = "Order, inventory, and payment each committed in their own database. The order is Confirmed.",
                OrderId = order.Id,
                CurrentState = await _state.ReadAsync(order.Id, cancellationToken)
            });
    }

    private async Task<PlacementResult> StoppedAsync(int orderId, string summary, CancellationToken cancellationToken)
    {
        return new PlacementResult(
            StatusCodes.Status409Conflict,
            new PlaceOrderResponse
            {
                Completed = false,
                Outcome = PlaceOrderOutcomes.StoppedAfterCommit,
                Summary = summary,
                OrderId = orderId,
                CurrentState = await _state.ReadAsync(orderId, cancellationToken)
            });
    }
}

public sealed record PlacementResult(int StatusCode, PlaceOrderResponse Response);
