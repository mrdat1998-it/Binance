using BinanceTracker.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

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

        var utcConverter = new ValueConverter<DateTime, DateTime>(
            v => v.Kind == DateTimeKind.Utc ? v : DateTime.SpecifyKind(v, DateTimeKind.Utc),
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

        var nullableUtcConverter = new ValueConverter<DateTime?, DateTime?>(
            v => !v.HasValue ? v : (v.Value.Kind == DateTimeKind.Utc ? v : DateTime.SpecifyKind(v.Value, DateTimeKind.Utc)),
            v => !v.HasValue ? v : DateTime.SpecifyKind(v.Value, DateTimeKind.Utc));

        modelBuilder.Entity<TradeSignalEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Symbol);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.StartTime);
            entity.HasIndex(e => new { e.Symbol, e.Status });

            entity.Property(e => e.StartTime).HasConversion(utcConverter);
            entity.Property(e => e.EndTime).HasConversion(nullableUtcConverter);
            entity.Property(e => e.CreatedAt).HasConversion(utcConverter);
            entity.Property(e => e.UpdatedAt).HasConversion(nullableUtcConverter);
        });
    }
}

