using System.Data.Common;
using Microsoft.EntityFrameworkCore;
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
    /// <summary>Response status when the order could not be created, so no saga exists.</summary>
    public const string NotStarted = "NotStarted";

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
        // Demo shortcut: the caller sends the amount to charge. A real system prices the order on the server.
        var created = await _orders.CreateAsync(request.ProductId, request.Quantity, request.Amount, cancellationToken);
        if (!created.Succeeded || created.OrderId is null)
        {
            return SagaResult.Stop(
                created.StatusCode == 0 ? StatusCodes.Status503ServiceUnavailable : created.StatusCode,
                new PlaceOrderResponse
                {
                    Status = NotStarted,
                    Summary = created.OutcomeUnknown
                        ? $"{created.Detail} The order may have been created, but no saga is tracking it."
                        : created.Detail
                });
        }

        var saga = new SagaInstance
        {
            OrderId = created.OrderId.Value,
            Status = SagaStatus.Running
        };
        _db.Sagas.Add(saga);
        if (!await TrySaveAsync(cancellationToken))
        {
            // Without a saga row, nothing would ever find this order again, so undo it now.
            var cancelOrphan = await _orders.CancelAsync(saga.OrderId, cancellationToken);
            return SagaResult.Stop(
                StatusCodes.Status503ServiceUnavailable,
                new PlaceOrderResponse
                {
                    OrderId = saga.OrderId,
                    Status = cancelOrphan.Succeeded ? SagaStatus.Compensated.ToString() : SagaStatus.Running.ToString(),
                    Summary = cancelOrphan.Succeeded
                        ? $"The saga state could not be saved, so order {saga.OrderId} was cancelled."
                        : $"The saga state could not be saved, and cancelling order {saga.OrderId} also failed: {cancelOrphan.Detail}"
                });
        }

        var reserved = await _inventory.ReserveAsync(saga.OrderId, request.ProductId, request.Quantity, cancellationToken);
        if (reserved.OutcomeUnknown)
        {
            return StillRunning(
                saga.OrderId,
                $"Inventory did not answer ({reserved.Detail}). The stock may or may not be reserved, so nothing was undone.");
        }

        if (!reserved.Succeeded)
        {
            var cancel = await _orders.CancelAsync(saga.OrderId, cancellationToken);
            if (!cancel.Succeeded)
            {
                return StillRunning(
                    saga.OrderId,
                    $"Inventory failed ({reserved.Detail}). Cancel also failed: {cancel.Detail}");
            }

            return await FinishAsync(
                saga,
                SagaStatus.Compensated,
                StatusCodes.Status409Conflict,
                $"Inventory failed. Order {saga.OrderId} was cancelled. Stock was not reserved. {reserved.Detail}",
                cancellationToken);
        }

        var charged = await _payments.ChargeAsync(saga.OrderId, request.Amount, cancellationToken);
        if (charged.OutcomeUnknown)
        {
            // The customer may already have been charged. Never release stock or cancel a possibly paid order.
            return StillRunning(
                saga.OrderId,
                $"Payment did not answer ({charged.Detail}). The customer may have been charged, so nothing was undone.");
        }

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

            return await FinishAsync(
                saga,
                SagaStatus.Compensated,
                StatusCodes.Status409Conflict,
                $"Payment failed. Stock was released and order {saga.OrderId} was cancelled. The payment row stays Failed. {charged.Detail}",
                cancellationToken);
        }

        var confirmed = await _orders.ConfirmAsync(saga.OrderId, cancellationToken);
        if (!confirmed.Succeeded)
        {
            return StillRunning(saga.OrderId, $"Payment succeeded, but confirm failed: {confirmed.Detail}");
        }

        return await FinishAsync(
            saga,
            SagaStatus.Completed,
            StatusCodes.Status200OK,
            $"Order {saga.OrderId} is confirmed. Stock is reserved and payment is completed.",
            cancellationToken);
    }

    /// <summary>
    /// Records the final saga status. The business outcome has already happened in the other services,
    /// so a failed write here is reported, not thrown.
    /// </summary>
    private async Task<SagaResult> FinishAsync(
        SagaInstance saga,
        SagaStatus status,
        int statusCode,
        string summary,
        CancellationToken cancellationToken)
    {
        saga.Status = status;
        var saved = await TrySaveAsync(cancellationToken);

        return SagaResult.Stop(
            statusCode,
            new PlaceOrderResponse
            {
                OrderId = saga.OrderId,
                Status = status.ToString(),
                Summary = saved
                    ? summary
                    : $"{summary} The saga row could not be updated and still shows Running."
            });
    }

    private async Task<bool> TrySaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (Exception exception) when (exception is DbUpdateException or DbException)
        {
            return false;
        }
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
