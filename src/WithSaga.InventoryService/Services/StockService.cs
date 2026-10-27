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
        var quantity = request.Quantity;
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        // One conditional UPDATE: the database checks the stock and changes it in the same statement,
        // so two concurrent orders cannot both take the last unit.
        var updated = await _db.Items
            .Where(row => row.ProductId == productId && row.AvailableQuantity >= quantity)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(row => row.AvailableQuantity, row => row.AvailableQuantity - quantity)
                .SetProperty(row => row.ReservedQuantity, row => row.ReservedQuantity + quantity),
                cancellationToken);

        if (updated == 0)
        {
            var exists = await _db.Items.AnyAsync(row => row.ProductId == productId, cancellationToken);
            return exists
                ? StockOperationResult.Fail(StatusCodes.Status409Conflict, $"Not enough stock for product '{productId}'. Stock was not changed.")
                : StockOperationResult.Fail(StatusCodes.Status404NotFound, $"Product '{productId}' was not found.");
        }

        _db.Reservations.Add(new StockReservation
        {
            OrderId = request.OrderId,
            ProductId = productId,
            Quantity = request.Quantity,
            ReservedAtUtc = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var available = await ReadAvailableAsync(productId, cancellationToken);
        return StockOperationResult.Ok(
            $"Reserved {quantity} unit(s) of product {productId} for order {request.OrderId}. Available is now {available}. Committed in the Inventory database.");
    }

    public async Task<StockOperationResult> ReleaseAsync(StockRequest request, CancellationToken cancellationToken)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        var reservation = await _db.Reservations.FirstOrDefaultAsync(row => row.OrderId == request.OrderId, cancellationToken);
        if (reservation is null)
        {
            return StockOperationResult.Fail(StatusCodes.Status404NotFound, $"No reservation exists for order {request.OrderId}.");
        }

        var quantity = reservation.Quantity;
        var updated = await _db.Items
            .Where(row => row.ProductId == reservation.ProductId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(row => row.AvailableQuantity, row => row.AvailableQuantity + quantity)
                .SetProperty(row => row.ReservedQuantity, row => row.ReservedQuantity - quantity),
                cancellationToken);

        if (updated == 0)
        {
            return StockOperationResult.Fail(StatusCodes.Status404NotFound, $"Product '{reservation.ProductId}' was not found.");
        }

        _db.Reservations.Remove(reservation);

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var available = await ReadAvailableAsync(reservation.ProductId, cancellationToken);
        return StockOperationResult.Ok(
            $"Released {quantity} unit(s) of product {reservation.ProductId} for order {request.OrderId}. Available is now {available}. Committed in the Inventory database.");
    }

    private Task<int> ReadAvailableAsync(string productId, CancellationToken cancellationToken)
    {
        return _db.Items.AsNoTracking()
            .Where(row => row.ProductId == productId)
            .Select(row => row.AvailableQuantity)
            .FirstAsync(cancellationToken);
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
