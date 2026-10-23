namespace WithSaga.InventoryService.Models;

/// <summary>
/// Stock row owned by the Inventory database.
/// </summary>
public class InventoryItem
{
    public int Id { get; set; }

    public string ProductId { get; set; } = "";

    public string ProductName { get; set; } = "";

    public int AvailableQuantity { get; set; }

    public int ReservedQuantity { get; set; }
}
