using Microsoft.EntityFrameworkCore;
using WithSaga.Orchestrator.Models;

namespace WithSaga.Orchestrator.Data;

/// <summary>
/// Commits only the orchestrator database (WithSaga_Orchestrator).
/// </summary>
public class OrchestratorDbContext : DbContext
{
    public OrchestratorDbContext(DbContextOptions<OrchestratorDbContext> options)
        : base(options)
    {
    }

    public DbSet<SagaInstance> Sagas => Set<SagaInstance>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SagaInstance>(entity =>
        {
            entity.Property(saga => saga.Status).HasConversion<string>().HasMaxLength(32);
            entity.HasIndex(saga => saga.OrderId).IsUnique();
        });
    }
}
