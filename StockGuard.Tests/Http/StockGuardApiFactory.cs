using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using StockGuard.Infrastructure.Data;
using StockGuard.Tests.Integration;

namespace StockGuard.Tests.Http;

public sealed class StockGuardApiFactory
    : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(
        IWebHostBuilder builder)
    {
        TestDatabase.CreateOptions();

        builder.UseEnvironment("Testing");
    }

    public void VerifyDatabaseIsolation()
    {
        using var scope = Services.CreateScope();

        var context = scope.ServiceProvider
            .GetRequiredService<StockGuardDbContext>();

        var connection = new NpgsqlConnectionStringBuilder(
            context.Database.GetDbConnection().ConnectionString);

        if (connection.Database != "stockguard_test")
        {
            throw new InvalidOperationException(
                "HTTP tests must use stockguard_test.");
        }
    }
}
