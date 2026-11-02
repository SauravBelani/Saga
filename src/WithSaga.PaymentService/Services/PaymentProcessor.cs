using Microsoft.EntityFrameworkCore;
using Npgsql;
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
        // A repeat for the same order gets the original answer instead of a second charge.
        var existing = await _db.Payments.AsNoTracking()
            .FirstOrDefaultAsync(row => row.OrderId == request.OrderId, cancellationToken);

        if (existing is not null)
        {
            return Replay(existing);
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
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Another request for this order saved its payment first (unique index on OrderId). Answer with that result.
            var original = await _db.Payments.AsNoTracking()
                .SingleAsync(row => row.OrderId == request.OrderId, cancellationToken);
            return Replay(original);
        }

        if (simulateFailure)
        {
            // A declined card is an expected business outcome, so it is a 4xx. A 5xx would mean "the server broke".
            return ChargeOutcome.FromPayment(
                StatusCodes.Status402PaymentRequired,
                Map(payment, $"Simulated payment decline for order {payment.OrderId}. Payment {payment.Id} is saved as Failed in the Payment database."));
        }

        return ChargeOutcome.FromPayment(
            StatusCodes.Status200OK,
            Map(payment, $"Charged {payment.Amount:0.00} for order {payment.OrderId}. Payment {payment.Id} is Completed and committed in the Payment database."));
    }

    private static ChargeOutcome Replay(Payment payment) => ChargeOutcome.FromPayment(
        payment.Status == PaymentStatus.Completed ? StatusCodes.Status200OK : StatusCodes.Status402PaymentRequired,
        Map(payment, $"Order {payment.OrderId} was already processed. Payment {payment.Id} is {payment.Status}."));

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
