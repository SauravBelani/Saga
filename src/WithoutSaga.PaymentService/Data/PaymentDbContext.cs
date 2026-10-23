using Microsoft.EntityFrameworkCore;
using WithoutSaga.PaymentService.Models;

namespace WithoutSaga.PaymentService.Data;

/// <summary>
/// Commits only the Payment database (SagaDemo_Payment).
/// </summary>
public class PaymentDbContext : DbContext
{
    public PaymentDbContext(DbContextOptions<PaymentDbContext> options)
        : base(options)
    {
    }

    public DbSet<Payment> Payments => Set<Payment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Payment>(entity =>
        {
            entity.Property(payment => payment.Amount).HasPrecision(18, 2);
            entity.Property(payment => payment.Status).HasConversion<string>().HasMaxLength(32);
            entity.HasIndex(payment => payment.OrderId).IsUnique();
        });
    }
}
