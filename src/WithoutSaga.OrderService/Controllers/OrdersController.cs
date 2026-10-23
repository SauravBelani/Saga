using Microsoft.AspNetCore.Mvc;
using WithoutSaga.OrderService.Contracts;
using WithoutSaga.OrderService.Services;

namespace WithoutSaga.OrderService.Controllers;

[ApiController]
[Route("api/orders")]
[Produces("application/json")]
public class OrdersController : ControllerBase
{
    private readonly OrderPlacementService _placement;

    public OrdersController(OrderPlacementService placement)
    {
        _placement = placement;
    }

    /// <summary>
    /// Places an order by calling Inventory and Payment.
    /// Each service commits its own database. If payment fails, the order stays Created and the stock stays reserved.
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
        var result = await _placement.PlaceAsync(request, cancellationToken);
        return StatusCode(result.StatusCode, result.Response);
    }
}
