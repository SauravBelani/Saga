using System.ComponentModel.DataAnnotations;

namespace WithSaga.OrderService.Contracts;

public class CreateOrderRequest
{
    [Required]
    [MaxLength(64)]
    public string ProductId { get; set; } = "";

    [Range(1, 1000)]
    public int Quantity { get; set; }

    [Range(0.01, 1000000)]
    public decimal Amount { get; set; }
}

public class OrderResponse
{
    public string Message { get; set; } = "";

    public int OrderId { get; set; }

    public string Status { get; set; } = "";
}

public class ApiMessage
{
    public string Message { get; set; } = "";
}
