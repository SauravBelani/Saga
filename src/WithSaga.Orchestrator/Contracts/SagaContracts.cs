using System.ComponentModel.DataAnnotations;

namespace WithSaga.Orchestrator.Contracts;

public class CreateOrderRequest
{
    /// <summary>Product id in the Inventory database. The demo seed is A.</summary>
    /// <example>A</example>
    [Required]
    [MaxLength(64)]
    public string ProductId { get; set; } = "";

    /// <summary>Units to reserve.</summary>
    /// <example>1</example>
    [Range(1, 1000)]
    public int Quantity { get; set; }

    /// <summary>
    /// Amount the Payment service will charge. Demo shortcut: a real system computes the price on the server
    /// and never charges an amount the caller chose.
    /// </summary>
    /// <example>49.99</example>
    [Range(0.01, 1000000)]
    public decimal Amount { get; set; }
}

public class PlaceOrderResponse
{
    public int? OrderId { get; set; }

    /// <summary>
    /// Completed, Compensated, Running when the saga could not reach a clean end,
    /// or NotStarted when the order could not be created.
    /// </summary>
    public string Status { get; set; } = "";

    public string Summary { get; set; } = "";
}
