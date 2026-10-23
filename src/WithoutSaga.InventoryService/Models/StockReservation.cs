namespace WithoutSaga.InventoryService.Models;

/// <summary>
/// Stock held for one order. Phase 1 never releases this row.
/// </summary>
public class StockReservation
{
    public int Id { get; set; }

    public int OrderId { get; set; }

    public string ProductId { get; set; } = "";

    public int Quantity { get; set; }

    public DateTime ReservedAtUtc { get; set; }
}
