using BinanceTracker.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace BinanceTracker.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<TradeSignalEntity> TradeSignals => Set<TradeSignalEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<TradeSignalEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Symbol);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.StartTime);
            entity.HasIndex(e => new { e.Symbol, e.Status });
        });
    }
}
