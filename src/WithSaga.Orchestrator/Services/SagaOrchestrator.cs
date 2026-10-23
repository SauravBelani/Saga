using WithSaga.Orchestrator.Clients;
using WithSaga.Orchestrator.Contracts;
using WithSaga.Orchestrator.Data;
using WithSaga.Orchestrator.Models;

namespace WithSaga.Orchestrator.Services;

/// <summary>
/// Runs the order saga. Each service commits its own database. Compensation is a new call, not a rollback.
/// </summary>
public class SagaOrchestrator
{
    private readonly OrchestratorDbContext _db;
    private readonly OrderClient _orders;
    private readonly InventoryClient _inventory;
    private readonly PaymentClient _payments;

    public SagaOrchestrator(
        OrchestratorDbContext db,
        OrderClient orders,
        InventoryClient inventory,
        PaymentClient payments)
    {
        _db = db;
        _orders = orders;
        _inventory = inventory;
        _payments = payments;
    }

    public async Task<SagaResult> PlaceAsync(CreateOrderRequest request, CancellationToken cancellationToken)
    {
        var created = await _orders.CreateAsync(request.ProductId, request.Quantity, request.Amount, cancellationToken);
        if (!created.Succeeded || created.OrderId is null)
        {
            return SagaResult.Stop(
                created.StatusCode == 0 ? StatusCodes.Status503ServiceUnavailable : created.StatusCode,
                new PlaceOrderResponse
                {
                    Status = SagaStatus.Running.ToString(),
                    Summary = created.Detail
                });
        }

        var saga = new SagaInstance
        {
            OrderId = created.OrderId.Value,
            Status = SagaStatus.Running
        };
        _db.Sagas.Add(saga);
        await _db.SaveChangesAsync(cancellationToken);

        var reserved = await _inventory.ReserveAsync(saga.OrderId, request.ProductId, request.Quantity, cancellationToken);
        if (!reserved.Succeeded)
        {
            var cancel = await _orders.CancelAsync(saga.OrderId, cancellationToken);
            if (!cancel.Succeeded)
            {
                return StillRunning(
                    saga.OrderId,
                    $"Inventory failed ({reserved.Detail}). Cancel also failed: {cancel.Detail}");
            }

            saga.Status = SagaStatus.Compensated;
            await _db.SaveChangesAsync(cancellationToken);
            return SagaResult.Stop(
                StatusCodes.Status409Conflict,
                new PlaceOrderResponse
                {
                    OrderId = saga.OrderId,
                    Status = saga.Status.ToString(),
                    Summary = $"Inventory failed. Order {saga.OrderId} was cancelled. Stock was not reserved. {reserved.Detail}"
                });
        }

        var charged = await _payments.ChargeAsync(saga.OrderId, request.Amount, cancellationToken);
        if (!charged.Succeeded)
        {
            var released = await _inventory.ReleaseAsync(saga.OrderId, request.ProductId, request.Quantity, cancellationToken);
            if (!released.Succeeded)
            {
                return StillRunning(
                    saga.OrderId,
                    $"Payment failed ({charged.Detail}). Release also failed: {released.Detail}");
            }

            var cancel = await _orders.CancelAsync(saga.OrderId, cancellationToken);
            if (!cancel.Succeeded)
            {
                return StillRunning(
                    saga.OrderId,
                    $"Payment failed ({charged.Detail}). Stock was released. Cancel also failed: {cancel.Detail}");
            }

            saga.Status = SagaStatus.Compensated;
            await _db.SaveChangesAsync(cancellationToken);
            return SagaResult.Stop(
                StatusCodes.Status409Conflict,
                new PlaceOrderResponse
                {
                    OrderId = saga.OrderId,
                    Status = saga.Status.ToString(),
                    Summary = $"Payment failed. Stock was released and order {saga.OrderId} was cancelled. The payment row stays Failed. {charged.Detail}"
                });
        }

        var confirmed = await _orders.ConfirmAsync(saga.OrderId, cancellationToken);
        if (!confirmed.Succeeded)
        {
            return StillRunning(saga.OrderId, $"Payment succeeded, but confirm failed: {confirmed.Detail}");
        }

        saga.Status = SagaStatus.Completed;
        await _db.SaveChangesAsync(cancellationToken);
        return SagaResult.Stop(
            StatusCodes.Status200OK,
            new PlaceOrderResponse
            {
                OrderId = saga.OrderId,
                Status = saga.Status.ToString(),
                Summary = $"Order {saga.OrderId} is confirmed. Stock is reserved and payment is completed."
            });
    }

    private static SagaResult StillRunning(int orderId, string summary)
    {
        return SagaResult.Stop(
            StatusCodes.Status500InternalServerError,
            new PlaceOrderResponse
            {
                OrderId = orderId,
                Status = SagaStatus.Running.ToString(),
                Summary = $"{summary} Saga stays Running."
            });
    }
}

public sealed class SagaResult
{
    public int StatusCode { get; private init; }

    public PlaceOrderResponse Response { get; private init; } = new();

    public static SagaResult Stop(int statusCode, PlaceOrderResponse response) => new()
    {
        StatusCode = statusCode,
        Response = response
    };
}
