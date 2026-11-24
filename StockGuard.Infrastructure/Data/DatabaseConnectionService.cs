using Microsoft.EntityFrameworkCore;

namespace StockGuard.Infrastructure.Data;

public class DatabaseConnectionService
{
    public async Task<bool> TestConnectionAsync()
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

        await using var context =
            new StockGuardDbContext(options);

        await context.Database.OpenConnectionAsync();

        await context.Database.CloseConnectionAsync();

        return true;
    }
}