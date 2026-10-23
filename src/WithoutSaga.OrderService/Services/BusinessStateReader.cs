using Microsoft.EntityFrameworkCore;
using WithoutSaga.OrderService.Clients;
using WithoutSaga.OrderService.Contracts;
using WithoutSaga.OrderService.Data;
using WithoutSaga.OrderService.Models;

namespace WithoutSaga.OrderService.Services;

/// <summary>
/// Reads the three databases through their own services so the demo can show the business state in one response.
/// </summary>
public class BusinessStateReader
{
    private readonly OrderDbContext _db;
    private readonly InventoryClient _inventory;
    private readonly PaymentClient _payment;

    public BusinessStateReader(OrderDbContext db, InventoryClient inventory, PaymentClient payment)
    {
        _db = db;
        _inventory = inventory;
        _payment = payment;
    }

    public async Task<CurrentState?> ReadAsync(int orderId, CancellationToken cancellationToken)
    {
        var order = await _db.Orders.AsNoTracking()
            .FirstOrDefaultAsync(row => row.Id == orderId, cancellationToken);

        if (order is null)
        {
            return null;
        }

        var product = await _inventory.GetProductAsync(order.ProductId, cancellationToken);
        var payment = await _payment.GetByOrderAsync(orderId, cancellationToken);

        var reservedForThisOrder = product.Body?.Reservations
            .Where(reservation => reservation.OrderId == orderId)
            .Sum(reservation => reservation.Quantity);

        var state = new CurrentState
        {
            OrderId = order.Id,
            OrderStatus = order.Status.ToString(),
            ProductId = order.ProductId,
            Quantity = order.Quantity,
            Amount = order.Amount,
            CreatedAtUtc = order.CreatedAtUtc,
            AvailableQuantity = product.Succeeded ? product.Body?.AvailableQuantity : null,
            ReservedQuantity = product.Succeeded ? product.Body?.ReservedQuantity : null,
            ReservedForThisOrder = product.Succeeded ? reservedForThisOrder : null,
            PaymentStatus = payment.StatusCode == StatusCodes.Status404NotFound ? null : payment.Body?.Status,
            PaymentAmount = payment.Body?.Amount
        };

        state.Explanation = Explain(state, product.ReachedService);
        return state;
    }

    private static string Explain(CurrentState state, bool inventoryReached)
    {
        if (!inventoryReached)
        {
            return "The order row is in the Order database. The Inventory service could not be read.";
        }

        if (state.OrderStatus == nameof(OrderStatus.Confirmed) && state.PaymentStatus == nameof(PaymentStatusName.Completed))
        {
            return "Consistent. The order is Confirmed, payment is Completed, and the reserved stock stays allocated to this order.";
        }

        if (state.OrderStatus == nameof(OrderStatus.Created)
            && state.ReservedForThisOrder > 0
            && state.PaymentStatus == nameof(PaymentStatusName.Failed))
        {
            return "Inconsistent. The order is still Created, stock is still reserved, and payment is Failed. Each of those commits remains in its own database.";
        }

        if (state.OrderStatus == nameof(OrderStatus.Created) && state.ReservedForThisOrder > 0 && state.PaymentStatus is null)
        {
            return "Inconsistent. The order is Created and stock is reserved. Payment has no row.";
        }

        if (state.OrderStatus == nameof(OrderStatus.Created) && state.ReservedForThisOrder is 0)
        {
            return "Inconsistent. The order row is Created and no stock is reserved for it.";
        }

        return $"Order is {state.OrderStatus}. Payment is {state.PaymentStatus ?? "missing"}.";
    }

    private static class PaymentStatusName
    {
        public const string Completed = "Completed";
        public const string Failed = "Failed";
    }
}
