using System.ComponentModel.DataAnnotations;

namespace WithSaga.InventoryService.Contracts;

public class StockRequest
{
    [Range(1, int.MaxValue)]
    public int OrderId { get; set; }

    [Required]
    [MaxLength(64)]
    public string ProductId { get; set; } = "";

    [Range(1, 1000)]
    public int Quantity { get; set; }
}

public class ApiMessage
{
    public string Message { get; set; } = "";
}
