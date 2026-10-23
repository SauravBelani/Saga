namespace WithoutSaga.OrderService.Data;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        await db.Database.EnsureCreatedAsync();
    }
}
