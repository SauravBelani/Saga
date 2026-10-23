using Microsoft.AspNetCore.Mvc;
using WithoutSaga.PaymentService.Contracts;
using WithoutSaga.PaymentService.Services;

namespace WithoutSaga.PaymentService.Controllers;

[ApiController]
[Route("api/payments")]
[Produces("application/json")]
public class PaymentsController : ControllerBase
{
    private readonly PaymentProcessor _payments;

    public PaymentsController(PaymentProcessor payments)
    {
        _payments = payments;
    }

    /// <summary>
    /// Charges the customer and commits the result in the Payment database.
    /// With simulated failure on, the Failed row is saved and this action returns HTTP 500.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(PaymentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(PaymentResponse), StatusCodes.Status500InternalServerError)]
    [ProducesResponseType(typeof(ApiMessage), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Charge(ChargePaymentRequest request, CancellationToken cancellationToken)
    {
        var result = await _payments.ChargeAsync(request, cancellationToken);
        if (result.Payment is not null)
        {
            return StatusCode(result.StatusCode, result.Payment);
        }

        return StatusCode(result.StatusCode, new ApiMessage { Message = result.ErrorMessage ?? "Payment failed." });
    }

    /// <summary>Reads the payment row for one order, if one was committed.</summary>
    [HttpGet("by-order/{orderId:int}")]
    [ProducesResponseType(typeof(PaymentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiMessage), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PaymentResponse>> GetByOrder(int orderId, CancellationToken cancellationToken)
    {
        var payment = await _payments.GetByOrderAsync(orderId, cancellationToken);
        if (payment is null)
        {
            return NotFound(new ApiMessage { Message = $"No payment exists for order {orderId}." });
        }

        return Ok(payment);
    }
}
