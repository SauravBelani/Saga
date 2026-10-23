using Microsoft.EntityFrameworkCore;
using WithSaga.InventoryService.Models;

namespace WithSaga.InventoryService.Data;

/// <summary>
/// Commits only the Inventory database (WithSaga_Inventory).
/// </summary>
public class InventoryDbContext : DbContext
{
    public InventoryDbContext(DbContextOptions<InventoryDbContext> options)
        : base(options)
    {
    }

    public DbSet<InventoryItem> Items => Set<InventoryItem>();

    public DbSet<StockReservation> Reservations => Set<StockReservation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<InventoryItem>(entity =>
        {
            entity.Property(item => item.ProductId).HasMaxLength(64).IsRequired();
            entity.Property(item => item.ProductName).HasMaxLength(128).IsRequired();
            entity.HasIndex(item => item.ProductId).IsUnique();
        });

        modelBuilder.Entity<StockReservation>(entity =>
        {
            entity.Property(reservation => reservation.ProductId).HasMaxLength(64).IsRequired();
            entity.HasIndex(reservation => reservation.OrderId).IsUnique();
        });
    }
}
