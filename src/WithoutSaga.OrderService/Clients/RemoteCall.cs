namespace WithoutSaga.OrderService.Clients;

public sealed class RemoteCall<T>
{
    public bool Succeeded { get; init; }

    /// <summary>0 means the HTTP call never reached the other service.</summary>
    public int StatusCode { get; init; }

    public string Detail { get; init; } = "";

    public T? Body { get; init; }

    public bool ReachedService => StatusCode != 0;

    public static RemoteCall<T> Unreachable(string detail) => new()
    {
        Succeeded = false,
        StatusCode = 0,
        Detail = detail
    };
}
