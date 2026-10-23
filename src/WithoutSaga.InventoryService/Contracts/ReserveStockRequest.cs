using System.ComponentModel.DataAnnotations;

namespace WithoutSaga.InventoryService.Contracts;

public class ReserveStockRequest
{
    /// <summary>Order id already committed by the Order service.</summary>
    /// <example>1</example>
    [Range(1, int.MaxValue)]
    public int OrderId { get; set; }

    /// <summary>Seeded demo product.</summary>
    /// <example>A</example>
    [Required]
    [MaxLength(64)]
    public string ProductId { get; set; } = "";

    /// <example>1</example>
    public int Quantity { get; set; }
}
