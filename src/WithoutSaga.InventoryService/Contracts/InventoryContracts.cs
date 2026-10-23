namespace WithoutSaga.InventoryService.Contracts;

public class ReservationInfo
{
    public int OrderId { get; set; }

    public int Quantity { get; set; }

    public DateTime ReservedAtUtc { get; set; }
}

public class ProductResponse
{
    public string ProductId { get; set; } = "";

    public string ProductName { get; set; } = "";

    public int AvailableQuantity { get; set; }

    public int ReservedQuantity { get; set; }

    public List<ReservationInfo> Reservations { get; set; } = [];
}

public class ReserveStockResponse : ProductResponse
{
    public string Message { get; set; } = "";

    public int OrderId { get; set; }
}

public class ApiMessage
{
    public string Message { get; set; } = "";
}
