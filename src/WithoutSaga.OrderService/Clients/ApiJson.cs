using System.Text.Json;
using System.Text.Json.Serialization;

namespace WithoutSaga.OrderService.Clients;

internal static class ApiJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

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
            // The body is not JSON. Return it as the detail text.
        }

        return raw;
    }
}
