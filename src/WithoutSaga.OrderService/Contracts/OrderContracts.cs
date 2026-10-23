using System.ComponentModel.DataAnnotations;

namespace WithoutSaga.OrderService.Contracts;

public class CreateOrderRequest
{
    /// <summary>Product id in the Inventory database. The demo seed is A.</summary>
    /// <example>A</example>
    [Required]
    [MaxLength(64)]
    public string ProductId { get; set; } = "";

    /// <summary>Units to reserve.</summary>
    /// <example>1</example>
    public int Quantity { get; set; }

    /// <summary>Amount the Payment service will charge.</summary>
    /// <example>49.99</example>
    [Range(0.01, 1000000)]
    public decimal Amount { get; set; }
}

public static class PlaceOrderOutcomes
{
    public const string Completed = "Completed";

    public const string StoppedAfterCommit = "StoppedAfterCommit";
}

/// <summary>
/// Live reading across the three databases after the call.
/// Order status comes from the Order database. Stock comes from Inventory. Payment comes from Payment.
/// </summary>
public class CurrentState
{
    public int OrderId { get; set; }

    public string OrderStatus { get; set; } = "";

    public string ProductId { get; set; } = "";

    public int Quantity { get; set; }

    public decimal Amount { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public int? AvailableQuantity { get; set; }

    public int? ReservedQuantity { get; set; }

    public int? ReservedForThisOrder { get; set; }

    public string? PaymentStatus { get; set; }

    public decimal? PaymentAmount { get; set; }

    public string Explanation { get; set; } = "";
}

public class PlaceOrderResponse
{
    /// <summary>Phase 1 marker. Phase 2 will use a Saga orchestrator.</summary>
    public string Pattern { get; set; } = "WithoutSaga";

    public bool Completed { get; set; }

    /// <summary>Completed or StoppedAfterCommit.</summary>
    public string Outcome { get; set; } = "";

    public string Summary { get; set; } = "";

    public int? OrderId { get; set; }

    public CurrentState? CurrentState { get; set; }
}
