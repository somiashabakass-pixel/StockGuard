using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace StockGuard.Infrastructure.Data;

public class StockGuardDbContextFactory
    : IDesignTimeDbContextFactory<StockGuardDbContext>
{
    public StockGuardDbContext CreateDbContext(string[] args)
    {
        string connectionString =
            Environment.GetEnvironmentVariable(
                "STOCKGUARD_CONNECTION_STRING")
            ?? throw new InvalidOperationException(
                "StockGuard database connection is not configured.");

        var options =
            new DbContextOptionsBuilder<StockGuardDbContext>()
                .UseNpgsql(connectionString)
                .Options;

        return new StockGuardDbContext(options);
    }
}
