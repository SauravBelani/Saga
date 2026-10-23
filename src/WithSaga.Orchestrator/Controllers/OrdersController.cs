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
    [ProducesResponseType(typeof(PlaceOrderResponse), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PlaceOrderResponse>> Place(CreateOrderRequest request, CancellationToken cancellationToken)
    {
        var result = await _orchestrator.PlaceAsync(request, cancellationToken);
        return StatusCode(result.StatusCode, result.Response);
    }
}
