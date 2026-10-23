using Microsoft.EntityFrameworkCore;
using WithSaga.PaymentService.Contracts;
using WithSaga.PaymentService.Data;
using WithSaga.PaymentService.Models;

namespace WithSaga.PaymentService.Services;

/// <summary>
/// Charges a customer in the Payment database. It does not know about stock or order status.
/// </summary>
public class PaymentProcessor
{
    private readonly PaymentDbContext _db;
    private readonly IConfiguration _configuration;

    public PaymentProcessor(PaymentDbContext db, IConfiguration configuration)
    {
        _db = db;
        _configuration = configuration;
    }

    public async Task<ChargeOutcome> ChargeAsync(ChargePaymentRequest request, CancellationToken cancellationToken)
    {
        var existing = await _db.Payments.AsNoTracking()
            .AnyAsync(row => row.OrderId == request.OrderId, cancellationToken);

        if (existing)
        {
            return ChargeOutcome.Fail(
                StatusCodes.Status409Conflict,
                $"Order {request.OrderId} already has a payment row.");
        }

        var simulateFailure = _configuration.GetValue<bool>("Payment:SimulateFailure");
        var status = simulateFailure ? PaymentStatus.Failed : PaymentStatus.Completed;

        var payment = new Payment
        {
            OrderId = request.OrderId,
            Amount = request.Amount,
            Status = status,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.Payments.Add(payment);
        await _db.SaveChangesAsync(cancellationToken);

        if (simulateFailure)
        {
            return ChargeOutcome.FromPayment(
                StatusCodes.Status500InternalServerError,
                Map(payment, $"Simulated payment failure for order {payment.OrderId}. Payment {payment.Id} is saved as Failed in the Payment database."));
        }

        return ChargeOutcome.FromPayment(
            StatusCodes.Status200OK,
            Map(payment, $"Charged {payment.Amount:0.00} for order {payment.OrderId}. Payment {payment.Id} is Completed and committed in the Payment database."));
    }

    private static PaymentResponse Map(Payment payment, string message) => new()
    {
        Message = message,
        PaymentId = payment.Id,
        OrderId = payment.OrderId,
        Amount = payment.Amount,
        Status = payment.Status.ToString()
    };
}

public sealed class ChargeOutcome
{
    public int StatusCode { get; private init; }

    public string? ErrorMessage { get; private init; }

    public PaymentResponse? Payment { get; private init; }

    public static ChargeOutcome Fail(int statusCode, string message) => new()
    {
        StatusCode = statusCode,
        ErrorMessage = message
    };

    public static ChargeOutcome FromPayment(int statusCode, PaymentResponse payment) => new()
    {
        StatusCode = statusCode,
        Payment = payment
    };
}
