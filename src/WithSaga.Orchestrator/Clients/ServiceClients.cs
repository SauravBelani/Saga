using System.Net.Http.Json;
using System.Text.Json;

namespace WithSaga.Orchestrator.Clients;

public sealed class RemoteCall
{
    public bool Succeeded { get; init; }

    public int StatusCode { get; init; }

    public string Detail { get; init; } = "";

    public int? OrderId { get; init; }

    public static RemoteCall Unreachable(string detail) => new()
    {
        Succeeded = false,
        StatusCode = 0,
        Detail = detail
    };
}

internal static class ApiJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static string ExtractMessage(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "The service returned an empty body.";
        }

        try
        {
            using var document = JsonDocument.Parse(raw);
            if (document.RootElement.TryGetProperty("message", out var message)
                && message.ValueKind == JsonValueKind.String)
            {
                return message.GetString() ?? raw;
            }
        }
        catch (JsonException)
        {
        }

        return raw;
    }

    public static int? ExtractOrderId(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(raw);
            if (document.RootElement.TryGetProperty("orderId", out var orderId)
                && orderId.TryGetInt32(out var value))
            {
                return value;
            }
        }
        catch (JsonException)
        {
        }

        return null;
    }
}

public sealed class OrderClient
{
    private readonly HttpClient _http;

    public OrderClient(HttpClient http)
    {
        _http = http;
    }

    public Task<RemoteCall> CreateAsync(string productId, int quantity, decimal amount, CancellationToken cancellationToken)
    {
        var body = new { productId, quantity, amount };
        return SendAsync(
            () => _http.PostAsJsonAsync("api/orders", body, ApiJson.Options, cancellationToken),
            "Order",
            cancellationToken);
    }

    public Task<RemoteCall> ConfirmAsync(int orderId, CancellationToken cancellationToken)
    {
        return SendAsync(
            () => _http.PostAsync($"api/orders/{orderId}/confirm", content: null, cancellationToken),
            "Order",
            cancellationToken);
    }

    public Task<RemoteCall> CancelAsync(int orderId, CancellationToken cancellationToken)
    {
        return SendAsync(
            () => _http.PostAsync($"api/orders/{orderId}/cancel", content: null, cancellationToken),
            "Order",
            cancellationToken);
    }

    private Task<RemoteCall> SendAsync(Func<Task<HttpResponseMessage>> send, string serviceName, CancellationToken cancellationToken)
    {
        return HttpCalls.SendAsync(_http, send, serviceName, cancellationToken);
    }
}

public sealed class InventoryClient
{
    private readonly HttpClient _http;

    public InventoryClient(HttpClient http)
    {
        _http = http;
    }

    public Task<RemoteCall> ReserveAsync(int orderId, string productId, int quantity, CancellationToken cancellationToken)
    {
        var body = new { orderId, productId, quantity };
        return HttpCalls.SendAsync(
            _http,
            () => _http.PostAsJsonAsync("api/inventory/reserve", body, ApiJson.Options, cancellationToken),
            "Inventory",
            cancellationToken);
    }

    public Task<RemoteCall> ReleaseAsync(int orderId, string productId, int quantity, CancellationToken cancellationToken)
    {
        var body = new { orderId, productId, quantity };
        return HttpCalls.SendAsync(
            _http,
            () => _http.PostAsJsonAsync("api/inventory/release", body, ApiJson.Options, cancellationToken),
            "Inventory",
            cancellationToken);
    }
}

public sealed class PaymentClient
{
    private readonly HttpClient _http;

    public PaymentClient(HttpClient http)
    {
        _http = http;
    }

    public Task<RemoteCall> ChargeAsync(int orderId, decimal amount, CancellationToken cancellationToken)
    {
        var body = new { orderId, amount };
        return HttpCalls.SendAsync(
            _http,
            () => _http.PostAsJsonAsync("api/payments", body, ApiJson.Options, cancellationToken),
            "Payment",
            cancellationToken);
    }
}

internal static class HttpCalls
{
    public static async Task<RemoteCall> SendAsync(
        HttpClient http,
        Func<Task<HttpResponseMessage>> send,
        string serviceName,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await send();
            var raw = await response.Content.ReadAsStringAsync(cancellationToken);
            var detail = ApiJson.ExtractMessage(raw);

            return new RemoteCall
            {
                Succeeded = response.IsSuccessStatusCode,
                StatusCode = (int)response.StatusCode,
                Detail = detail,
                OrderId = ApiJson.ExtractOrderId(raw)
            };
        }
        catch (HttpRequestException exception)
        {
            return RemoteCall.Unreachable(
                $"{serviceName} service is not reachable at {http.BaseAddress}. Start WithSaga.{serviceName}Service. {exception.Message}");
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            return RemoteCall.Unreachable(
                $"{serviceName} service did not respond at {http.BaseAddress}. {exception.Message}");
        }
    }
}
