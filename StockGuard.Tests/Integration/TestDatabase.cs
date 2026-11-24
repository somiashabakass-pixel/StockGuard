using Microsoft.EntityFrameworkCore;
using Npgsql;
using StockGuard.Infrastructure.Data;

namespace StockGuard.Tests.Integration;

public static class TestDatabase
{
    public static DbContextOptions<StockGuardDbContext> CreateOptions()
    {
        string connectionString =
            Environment.GetEnvironmentVariable(
                "STOCKGUARD_TEST_CONNECTION_STRING")
            ?? throw new InvalidOperationException(
                "Test database connection is not configured.");

        var builder =
            new NpgsqlConnectionStringBuilder(connectionString);

        if (builder.Database != "stockguard_test")
        {
            throw new InvalidOperationException(
                "Integration tests must use stockguard_test.");
        }

        return new DbContextOptionsBuilder<StockGuardDbContext>()
            .UseNpgsql(connectionString)
            .Options;
    }

    public static StockGuardDbContext CreateContext()
    {
        return new StockGuardDbContext(CreateOptions());
    }
}
