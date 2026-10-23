using System.Net.Http.Json;
using System.Text.Json;

namespace WithoutSaga.OrderService.Clients;

public sealed class InventoryClient
{
    private readonly HttpClient _http;

    public InventoryClient(HttpClient http)
    {
        _http = http;
    }

    public Task<RemoteCall<ProductSnapshot>> GetProductAsync(string productId, CancellationToken cancellationToken)
    {
        var path = $"api/inventory/{Uri.EscapeDataString(productId)}";
        return SendAsync<ProductSnapshot>(() => _http.GetAsync(path, cancellationToken), cancellationToken);
    }

    public Task<RemoteCall<ReserveSnapshot>> ReserveAsync(int orderId, string productId, int quantity, CancellationToken cancellationToken)
    {
        var body = new { orderId, productId, quantity };
        return SendAsync<ReserveSnapshot>(
            () => _http.PostAsJsonAsync("api/inventory/reserve", body, ApiJson.Options, cancellationToken),
            cancellationToken);
    }

    private async Task<RemoteCall<T>> SendAsync<T>(Func<Task<HttpResponseMessage>> send, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await send();
            var raw = await response.Content.ReadAsStringAsync(cancellationToken);
            var detail = ApiJson.ExtractMessage(raw);

            if (!response.IsSuccessStatusCode)
            {
                return new RemoteCall<T>
                {
                    Succeeded = false,
                    StatusCode = (int)response.StatusCode,
                    Detail = detail
                };
            }

            var body = string.IsNullOrWhiteSpace(raw)
                ? default
                : JsonSerializer.Deserialize<T>(raw, ApiJson.Options);

            return new RemoteCall<T>
            {
                Succeeded = true,
                StatusCode = (int)response.StatusCode,
                Detail = string.IsNullOrWhiteSpace(detail) ? "Inventory call succeeded." : detail,
                Body = body
            };
        }
        catch (HttpRequestException exception)
        {
            return RemoteCall<T>.Unreachable(
                $"Inventory service is not reachable at {_http.BaseAddress}. Start WithoutSaga.InventoryService. {exception.Message}");
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            return RemoteCall<T>.Unreachable(
                $"Inventory service did not respond at {_http.BaseAddress}. {exception.Message}");
        }
    }
}

public class ProductSnapshot
{
    public string ProductId { get; set; } = "";

    public string ProductName { get; set; } = "";

    public int AvailableQuantity { get; set; }

    public int ReservedQuantity { get; set; }

    public List<ReservationSnapshot> Reservations { get; set; } = [];
}

public sealed class ReservationSnapshot
{
    public int OrderId { get; set; }

    public int Quantity { get; set; }
}

public sealed class ReserveSnapshot : ProductSnapshot
{
    public string Message { get; set; } = "";
}
