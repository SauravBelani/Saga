using System.ComponentModel.DataAnnotations;

namespace WithSaga.PaymentService.Contracts;

public class ChargePaymentRequest
{
    [Range(1, int.MaxValue)]
    public int OrderId { get; set; }

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
