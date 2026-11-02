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
        // Step 1: save the order in the Order database with status "Created".
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

        // Step 2: ask the Inventory Service (its own database) to reserve the stock.
        var reserve = await _inventory.ReserveAsync(order.Id, order.ProductId, order.Quantity, cancellationToken);
        if (!reserve.Succeeded)
        {
            // The order row is already committed, so it is simply left as "Created".
            return await StoppedAsync(
                order.Id,
                "Inventory reserve failed after the order row was committed. The order stays Created.",
                cancellationToken);
        }

        // Step 3: ask the Payment Service (also its own database) to charge the customer.
        var payment = await _payment.ChargeAsync(order.Id, order.Amount, cancellationToken);
        if (!payment.Succeeded)
        {
            // The order row and the reservation are already committed.
            // Nothing releases the stock or cancels the order.
            return await StoppedAsync(
                order.Id,
                "Payment failed. The order row and the stock reservation stay committed in their own databases.",
                cancellationToken);
        }

        // Step 4: everything worked, so mark the order as confirmed.
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
