using Microsoft.EntityFrameworkCore;

namespace WithSaga.InventoryService.Data;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        await db.Database.EnsureCreatedAsync();

        if (!await db.Items.AnyAsync())
        {
            db.Items.Add(DemoCatalog.CreateItem());
            await db.SaveChangesAsync();
        }
    }
}
