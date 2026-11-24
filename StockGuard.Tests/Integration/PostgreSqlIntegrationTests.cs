using Microsoft.EntityFrameworkCore;
using Xunit;

namespace StockGuard.Tests.Integration;

[Trait("Category", "Integration")]
public class PostgreSqlIntegrationTests
{
    [Fact]
    public async Task DatabaseConnection_ShouldSucceed()
    {
        await using var context = TestDatabase.CreateContext();

        bool connected = await context.Database.CanConnectAsync();

        Assert.True(connected);
    }

    [Fact]
    public async Task Database_ShouldBeTestDatabase()
    {
        await using var context = TestDatabase.CreateContext();

        string databaseName = await context.Database
            .SqlQueryRaw<string>(
                "SELECT current_database() AS \"Value\"")
            .SingleAsync();

        Assert.Equal("stockguard_test", databaseName);
    }

    [Fact]
    public async Task Database_ShouldHaveRequiredTables()
    {
        await using var context = TestDatabase.CreateContext();

        var tables = await context.Database
            .SqlQueryRaw<string>(
                """
                SELECT table_name::text AS "Value"
                FROM information_schema.tables
                WHERE table_schema = 'public'
                """)
            .ToListAsync();

        Assert.Contains("organizations", tables);
        Assert.Contains("stores", tables);
        Assert.Contains("warehouses", tables);
        Assert.Contains("audit_runs", tables);
        Assert.Contains("audit_results", tables);
        Assert.Contains("__EFMigrationsHistory", tables);
    }
}
