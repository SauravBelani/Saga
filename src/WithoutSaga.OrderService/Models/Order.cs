namespace WithoutSaga.OrderService.Models;

public enum OrderStatus
{
    Created,
    Confirmed
}

/// <summary>
/// Order row owned by the Order database.
/// </summary>
public class Order
{
    public int Id { get; set; }

    public string ProductId { get; set; } = "";

    public int Quantity { get; set; }

    public decimal Amount { get; set; }

    public OrderStatus Status { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
