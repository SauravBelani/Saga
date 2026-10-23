namespace WithSaga.Orchestrator.Data;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrchestratorDbContext>();
        await db.Database.EnsureCreatedAsync();
    }
}
