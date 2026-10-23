using System.ComponentModel.DataAnnotations;

namespace WithoutSaga.PaymentService.Contracts;

public class ChargePaymentRequest
{
    /// <example>1</example>
    [Range(1, int.MaxValue)]
    public int OrderId { get; set; }

    /// <example>49.99</example>
    [Range(0.01, 1000000)]
    public decimal Amount { get; set; }
}

public class PaymentResponse
{
    public string Message { get; set; } = "";

    public int PaymentId { get; set; }

    public int OrderId { get; set; }

    public decimal Amount { get; set; }

    public string Status { get; set; } = "";
}

public class ApiMessage
{
    public string Message { get; set; } = "";
}
