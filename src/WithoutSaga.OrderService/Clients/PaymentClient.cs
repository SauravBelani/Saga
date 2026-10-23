using System.Net.Http.Json;
using System.Text.Json;

namespace WithoutSaga.OrderService.Clients;

public sealed class PaymentClient
{
    private readonly HttpClient _http;

    public PaymentClient(HttpClient http)
    {
        _http = http;
    }

    public Task<RemoteCall<PaymentSnapshot>> ChargeAsync(int orderId, decimal amount, CancellationToken cancellationToken)
    {
        var body = new { orderId, amount };
        return SendAsync<PaymentSnapshot>(
            () => _http.PostAsJsonAsync("api/payments", body, ApiJson.Options, cancellationToken),
            cancellationToken);
    }

    public Task<RemoteCall<PaymentSnapshot>> GetByOrderAsync(int orderId, CancellationToken cancellationToken)
    {
        return SendAsync<PaymentSnapshot>(
            () => _http.GetAsync($"api/payments/by-order/{orderId}", cancellationToken),
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
                Detail = string.IsNullOrWhiteSpace(detail) ? "Payment call succeeded." : detail,
                Body = body
            };
        }
        catch (HttpRequestException exception)
        {
            return RemoteCall<T>.Unreachable(
                $"Payment service is not reachable at {_http.BaseAddress}. Start WithoutSaga.PaymentService. {exception.Message}");
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            return RemoteCall<T>.Unreachable(
                $"Payment service did not respond at {_http.BaseAddress}. {exception.Message}");
        }
    }
}

public sealed class PaymentSnapshot
{
    public string Message { get; set; } = "";

    public int PaymentId { get; set; }

    public int OrderId { get; set; }

    public decimal Amount { get; set; }

    public string Status { get; set; } = "";
}
