namespace WithSaga.InventoryService.Models;

/// <summary>
/// Stock held for one order. Release deletes this row and returns the quantity.
/// </summary>
public class StockReservation
{
    public int Id { get; set; }

    public int OrderId { get; set; }

    public string ProductId { get; set; } = "";

    public int Quantity { get; set; }

    public DateTime ReservedAtUtc { get; set; }
}
