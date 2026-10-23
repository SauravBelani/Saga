using Microsoft.AspNetCore.Mvc;
using WithoutSaga.InventoryService.Contracts;
using WithoutSaga.InventoryService.Services;

namespace WithoutSaga.InventoryService.Controllers;

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

    /// <summary>Reads one product from the Inventory database.</summary>
    [HttpGet("{productId}")]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiMessage), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductResponse>> Get(string productId, CancellationToken cancellationToken)
    {
        var product = await _stock.GetAsync(productId, cancellationToken);
        if (product is null)
        {
            return NotFound(new ApiMessage { Message = $"Product '{productId}' was not found." });
        }

        return Ok(product);
    }

    /// <summary>
    /// Reserves stock and commits that change in the Inventory database.
    /// The caller is the Order service. This action does not take payment.
    /// </summary>
    [HttpPost("reserve")]
    [ProducesResponseType(typeof(ReserveStockResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiMessage), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiMessage), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Reserve(ReserveStockRequest request, CancellationToken cancellationToken)
    {
        var result = await _stock.ReserveAsync(request, cancellationToken);
        if (result.Body is null)
        {
            return StatusCode(result.StatusCode, new ApiMessage { Message = result.ErrorMessage ?? "Reserve failed." });
        }

        return Ok(result.Body);
    }
}
