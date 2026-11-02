using Microsoft.AspNetCore.Mvc;
using WithSaga.InventoryService.Contracts;
using WithSaga.InventoryService.Services;

namespace WithSaga.InventoryService.Controllers;

[ApiController]
[Route("api/inventory")]
[Produces("application/json")]
public class InventoryController : ControllerBase
{
    private readonly StockService _stock;

    public InventoryController(StockService stock)
    {
        _stock = stock;
    }

    /// <summary>
    /// Reserves stock and commits that change in the Inventory database.
    /// Returns 409 when there is not enough stock, or when Inventory:SimulateFailure is true (nothing is saved).
    /// A repeat for the same order returns 200 without taking stock again.
    /// </summary>
    [HttpPost("reserve")]
    [ProducesResponseType(typeof(ApiMessage), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiMessage), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiMessage), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Reserve(StockRequest request, CancellationToken cancellationToken)
    {
        var result = await _stock.ReserveAsync(request, cancellationToken);
        return StatusCode(result.StatusCode, new ApiMessage { Message = result.Message });
    }

    /// <summary>
    /// Returns reserved stock to available and deletes the reservation. Compensation after payment fails.
    /// Safe to retry: when no reservation is held for the order, it returns 200 and changes nothing.
    /// </summary>
    // Demo only: no authentication. In production, only the orchestrator should be able to call this.
    [HttpPost("release")]
    [ProducesResponseType(typeof(ApiMessage), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiMessage), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Release(StockRequest request, CancellationToken cancellationToken)
    {
        var result = await _stock.ReleaseAsync(request, cancellationToken);
        return StatusCode(result.StatusCode, new ApiMessage { Message = result.Message });
    }
}
