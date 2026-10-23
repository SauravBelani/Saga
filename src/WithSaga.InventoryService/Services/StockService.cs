using Microsoft.EntityFrameworkCore;
using WithSaga.InventoryService.Contracts;
using WithSaga.InventoryService.Data;
using WithSaga.InventoryService.Models;

namespace WithSaga.InventoryService.Services;

/// <summary>
/// Reserves and releases stock in the Inventory database. It does not call Payment or Order.
/// </summary>
public class StockService
{
    private readonly InventoryDbContext _db;
    private readonly IConfiguration _configuration;

    public StockService(InventoryDbContext db, IConfiguration configuration)
    {
        _db = db;
        _configuration = configuration;
    }

    public async Task<StockOperationResult> ReserveAsync(StockRequest request, CancellationToken cancellationToken)
    {
        bool isfailed = _configuration.GetValue<bool>("Inventory:SimulateFailure");

        if (isfailed)
        {
            return StockOperationResult.Fail(
                StatusCodes.Status500InternalServerError,
                $"Simulated inventory failure for order {request.OrderId}. Stock was not changed.");
        }

        var productId = request.ProductId.Trim();
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        var item = await _db.Items.FirstOrDefaultAsync(row => row.ProductId == productId, cancellationToken);
        if (item is null)
        {
            return StockOperationResult.Fail(StatusCodes.Status404NotFound, $"Product '{productId}' was not found.");
        }

        item.AvailableQuantity -= request.Quantity;
        item.ReservedQuantity += request.Quantity;

        _db.Reservations.Add(new StockReservation
        {
            OrderId = request.OrderId,
            ProductId = productId,
            Quantity = request.Quantity,
            ReservedAtUtc = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return StockOperationResult.Ok(
            $"Reserved {request.Quantity} unit(s) of product {productId} for order {request.OrderId}. Available is now {item.AvailableQuantity}. Committed in the Inventory database.");
    }

    public async Task<StockOperationResult> ReleaseAsync(StockRequest request, CancellationToken cancellationToken)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        var reservation = await _db.Reservations.FirstOrDefaultAsync(row => row.OrderId == request.OrderId, cancellationToken);
        if (reservation is null)
        {
            return StockOperationResult.Fail(StatusCodes.Status404NotFound, $"No reservation exists for order {request.OrderId}.");
        }

        var item = await _db.Items.FirstOrDefaultAsync(row => row.ProductId == reservation.ProductId, cancellationToken);
        if (item is null)
        {
            return StockOperationResult.Fail(StatusCodes.Status404NotFound, $"Product '{reservation.ProductId}' was not found.");
        }

        item.AvailableQuantity += reservation.Quantity;
        item.ReservedQuantity -= reservation.Quantity;
        _db.Reservations.Remove(reservation);

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return StockOperationResult.Ok(
            $"Released {reservation.Quantity} unit(s) of product {item.ProductId} for order {request.OrderId}. Available is now {item.AvailableQuantity}. Committed in the Inventory database.");
    }
}

public sealed class StockOperationResult
{
    public int StatusCode { get; private init; }

    public string Message { get; private init; } = "";

    public bool Succeeded => StatusCode == StatusCodes.Status200OK;

    public static StockOperationResult Ok(string message) => new()
    {
        StatusCode = StatusCodes.Status200OK,
        Message = message
    };

    public static StockOperationResult Fail(int statusCode, string message) => new()
    {
        StatusCode = statusCode,
        Message = message
    };
}
