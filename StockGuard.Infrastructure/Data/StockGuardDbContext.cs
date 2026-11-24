using Microsoft.EntityFrameworkCore;
using StockGuard.Infrastructure.Data.Entities;

namespace StockGuard.Infrastructure.Data;

public class StockGuardDbContext : DbContext
{
    public StockGuardDbContext(
        DbContextOptions<StockGuardDbContext> options)
        : base(options)
    {
    }

    public DbSet<Organization> Organizations
        => Set<Organization>();

    public DbSet<Store> Stores
        => Set<Store>();

    public DbSet<Warehouse> Warehouses
        => Set<Warehouse>();

    public DbSet<AuditRun> AuditRuns
        => Set<AuditRun>();

    public DbSet<AuditResultEntity> AuditResults
        => Set<AuditResultEntity>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(StockGuardDbContext).Assembly);
    }
}
