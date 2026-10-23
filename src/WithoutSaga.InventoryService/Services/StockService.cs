using Microsoft.EntityFrameworkCore;
using WithoutSaga.InventoryService.Contracts;
using WithoutSaga.InventoryService.Data;
using WithoutSaga.InventoryService.Models;

namespace WithoutSaga.InventoryService.Services;

/// <summary>
/// Reserves stock in the Inventory database. It does not call Payment or Order.
/// </summary>
public class StockService
{
    private readonly InventoryDbContext _db;

    public StockService(InventoryDbContext db)
    {
        _db = db;
    }

    public async Task<ProductResponse?> GetAsync(string productId, CancellationToken cancellationToken)
    {
        var item = await _db.Items.AsNoTracking()
            .FirstOrDefaultAsync(row => row.ProductId == productId, cancellationToken);

        if (item is null)
        {
            return null;
        }

        var reservations = await _db.Reservations.AsNoTracking()
            .Where(reservation => reservation.ProductId == productId)
            .ToListAsync(cancellationToken);

        return Map(item, reservations);
    }

    public async Task<StockOperationResult> ReserveAsync(ReserveStockRequest request, CancellationToken cancellationToken)
    {
        var productId = request.ProductId.Trim();

        // The counter change and the reservation row commit together, but only in this database.
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

        var reservations = await _db.Reservations.AsNoTracking()
            .Where(reservation => reservation.ProductId == productId)
            .ToListAsync(cancellationToken);

        var body = new ReserveStockResponse
        {
            Message = $"Reserved {request.Quantity} unit(s) of product {productId} for order {request.OrderId}. Available is now {item.AvailableQuantity}. Reserved is now {item.ReservedQuantity}. Committed in the Inventory database.",
            OrderId = request.OrderId,
            ProductId = item.ProductId,
            ProductName = item.ProductName,
            AvailableQuantity = item.AvailableQuantity,
            ReservedQuantity = item.ReservedQuantity,
            Reservations = reservations.Select(MapReservation).ToList()
        };

        return StockOperationResult.Ok(body);
    }

    private static ProductResponse Map(InventoryItem item, IEnumerable<StockReservation> reservations) => new()
    {
        ProductId = item.ProductId,
        ProductName = item.ProductName,
        AvailableQuantity = item.AvailableQuantity,
        ReservedQuantity = item.ReservedQuantity,
        Reservations = reservations.Select(MapReservation).ToList()
    };

    private static ReservationInfo MapReservation(StockReservation reservation) => new()
    {
        OrderId = reservation.OrderId,
        Quantity = reservation.Quantity,
        ReservedAtUtc = reservation.ReservedAtUtc
    };
}

public sealed class StockOperationResult
{
    public int StatusCode { get; private init; }

    public string? ErrorMessage { get; private init; }

    public ReserveStockResponse? Body { get; private init; }

    public static StockOperationResult Ok(ReserveStockResponse body) => new()
    {
        StatusCode = StatusCodes.Status200OK,
        Body = body
    };

    public static StockOperationResult Fail(int statusCode, string message) => new()
    {
        StatusCode = statusCode,
        ErrorMessage = message
    };
}
