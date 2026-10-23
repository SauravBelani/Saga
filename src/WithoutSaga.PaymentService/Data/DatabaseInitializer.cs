namespace WithoutSaga.PaymentService.Data;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
        await db.Database.EnsureCreatedAsync();
    }
}
