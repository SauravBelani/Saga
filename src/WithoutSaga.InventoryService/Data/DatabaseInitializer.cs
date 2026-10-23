using Microsoft.EntityFrameworkCore;

namespace WithoutSaga.InventoryService.Data;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();

        // Creates tables in this service's database only.
        await db.Database.EnsureCreatedAsync();

        if (!await db.Items.AnyAsync())
        {
            db.Items.Add(DemoCatalog.CreateItem());
            await db.SaveChangesAsync();
        }
    }
}
