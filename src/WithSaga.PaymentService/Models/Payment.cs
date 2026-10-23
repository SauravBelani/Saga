namespace WithSaga.PaymentService.Models;

public enum PaymentStatus
{
    Completed,
    Failed
}

/// <summary>
/// Payment row owned by the Payment database.
/// </summary>
public class Payment
{
    public int Id { get; set; }

    public int OrderId { get; set; }

    public decimal Amount { get; set; }

    public PaymentStatus Status { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
