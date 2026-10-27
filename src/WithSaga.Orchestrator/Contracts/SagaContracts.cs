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

    /// <summary>Amount the Payment service will charge.</summary>
    /// <example>49.99</example>
    [Range(0.01, 1000000)]
    public decimal Amount { get; set; }
}

public class PlaceOrderResponse
{
    public int? OrderId { get; set; }

    /// <summary>Completed, Compensated, or Running when compensation could not finish.</summary>
    public string Status { get; set; } = "";

    public string Summary { get; set; } = "";
}
