using Microsoft.AspNetCore.Mvc;
using WithSaga.OrderService.Contracts;
using WithSaga.OrderService.Services;

namespace WithSaga.OrderService.Controllers;

[ApiController]
[Route("api/orders")]
[Produces("application/json")]
public class OrdersController : ControllerBase
{
    private readonly OrderCommands _orders;

    public OrdersController(OrderCommands orders)
    {
        _orders = orders;
    }

    /// <summary>Creates an order as Created. Called by the orchestrator.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<OrderResponse>> Create(CreateOrderRequest request, CancellationToken cancellationToken)
    {
        var result = await _orders.CreateAsync(request, cancellationToken);
        return Ok(result.Body);
    }

    /// <summary>Sets the order to Confirmed. Called by the orchestrator after payment succeeds.</summary>
    [HttpPost("{id:int}/confirm")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiMessage), StatusCodes.Status404NotFound)]
    public Task<IActionResult> Confirm(int id, CancellationToken cancellationToken)
    {
        return Update(id, _orders.ConfirmAsync(id, cancellationToken));
    }

    /// <summary>Sets the order to Cancelled. Compensation after inventory or payment fails.</summary>
    [HttpPost("{id:int}/cancel")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiMessage), StatusCodes.Status404NotFound)]
    public Task<IActionResult> Cancel(int id, CancellationToken cancellationToken)
    {
        return Update(id, _orders.CancelAsync(id, cancellationToken));
    }

    private static async Task<IActionResult> Update(int id, Task<OrderCommandResult> operation)
    {
        var result = await operation;
        if (result.Body is null)
        {
            return new ObjectResult(new ApiMessage { Message = result.ErrorMessage ?? $"Order {id} was not updated." })
            {
                StatusCode = result.StatusCode
            };
        }

        return new OkObjectResult(result.Body);
    }
}
