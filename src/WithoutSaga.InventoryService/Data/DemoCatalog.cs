using WithoutSaga.InventoryService.Models;

namespace WithoutSaga.InventoryService.Data;

/// <summary>
/// The single product seeded for the demo.
/// </summary>
public static class DemoCatalog
{
    public const string ProductId = "A";

    public const string ProductName = "Product A";

    public const int InitialAvailableQuantity = 10;

    public static InventoryItem CreateItem() => new()
    {
        ProductId = ProductId,
        ProductName = ProductName,
        AvailableQuantity = InitialAvailableQuantity,
        ReservedQuantity = 0
    };
}
