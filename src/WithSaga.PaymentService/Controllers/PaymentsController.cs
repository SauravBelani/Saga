using Microsoft.AspNetCore.Mvc;
using WithSaga.PaymentService.Contracts;
using WithSaga.PaymentService.Services;

namespace WithSaga.PaymentService.Controllers;

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
    /// With simulated failure on, the Failed row is saved and this action returns HTTP 402 (declined).
    /// A repeat for the same order returns the original result.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(PaymentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(PaymentResponse), StatusCodes.Status402PaymentRequired)]
    public async Task<IActionResult> Charge(ChargePaymentRequest request, CancellationToken cancellationToken)
    {
        var result = await _payments.ChargeAsync(request, cancellationToken);
        if (result.Payment is not null)
        {
            return StatusCode(result.StatusCode, result.Payment);
        }

        return StatusCode(result.StatusCode, new ApiMessage { Message = result.ErrorMessage ?? "Payment failed." });
    }
}
