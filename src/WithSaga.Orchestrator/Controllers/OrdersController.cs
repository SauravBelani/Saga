using Microsoft.AspNetCore.Mvc;
using WithSaga.Orchestrator.Contracts;
using WithSaga.Orchestrator.Services;

namespace WithSaga.Orchestrator.Controllers;

[ApiController]
[Route("api/orders")]
[Produces("application/json")]
public class OrdersController : ControllerBase
{
    private readonly SagaOrchestrator _orchestrator;

    public OrdersController(SagaOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    /// <summary>
    /// Places an order through the saga orchestrator.
    /// Inventory failure cancels the order. Payment failure releases stock and then cancels the order.
    /// </summary>
    /// <remarks>
    /// Sample request:
    ///
    ///     POST /api/orders
    ///     {
    ///       "productId": "A",
    ///       "quantity": 1,
    ///       "amount": 49.99
    ///     }
    ///
    /// </remarks>
    [HttpPost]
    [ProducesResponseType(typeof(PlaceOrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(PlaceOrderResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(PlaceOrderResponse), StatusCodes.Status500InternalServerError)]
    [ProducesResponseType(typeof(PlaceOrderResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<PlaceOrderResponse>> Place(CreateOrderRequest request)
    {
        // The saga is not tied to the HTTP request, so a caller that disconnects cannot stop it halfway.
        // It does not survive this process crashing; that needs a step log and a recovery worker.
        var result = await _orchestrator.PlaceAsync(request, CancellationToken.None);
        return StatusCode(result.StatusCode, result.Response);
    }
}
