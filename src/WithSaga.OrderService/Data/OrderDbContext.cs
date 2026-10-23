using Microsoft.EntityFrameworkCore;
using WithSaga.OrderService.Models;

namespace WithSaga.OrderService.Data;

/// <summary>
/// Commits only the Order database (WithSaga_Order).
/// </summary>
public class OrderDbContext : DbContext
{
    public OrderDbContext(DbContextOptions<OrderDbContext> options)
        : base(options)
    {
    }

    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>(entity =>
        {
            entity.Property(order => order.ProductId).HasMaxLength(64).IsRequired();
            entity.Property(order => order.Amount).HasPrecision(18, 2);
            entity.Property(order => order.Status).HasConversion<string>().HasMaxLength(32);
        });
    }
}
