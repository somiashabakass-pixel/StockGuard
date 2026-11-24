using Microsoft.EntityFrameworkCore;
using StockGuard.Infrastructure.Data.Entities;

namespace StockGuard.Infrastructure.Data;

public class DevelopmentDataService
{
    public static readonly Guid OrganizationId =
        Guid.Parse("10000000-0000-0000-0000-000000000001");

    public static readonly Guid StoreId =
        Guid.Parse("20000000-0000-0000-0000-000000000001");

    public static readonly Guid WarehouseId =
        Guid.Parse("30000000-0000-0000-0000-000000000001");

    public async Task InitializeAsync()
    {
        string connectionString =
            Environment.GetEnvironmentVariable(
                "STOCKGUARD_CONNECTION_STRING")
            ?? throw new InvalidOperationException(
                "Database connection is not configured.");

        var options =
            new DbContextOptionsBuilder<StockGuardDbContext>()
                .UseNpgsql(connectionString)
                .Options;

        await using var context =
            new StockGuardDbContext(options);

        if (!await context.Organizations
            .AnyAsync(x => x.Id == OrganizationId))
        {
            context.Organizations.Add(new Organization
            {
                Id = OrganizationId,
                Name = "StockGuard Demo Company"
            });
        }

        if (!await context.Stores
            .AnyAsync(x => x.Id == StoreId))
        {
            context.Stores.Add(new Store
            {
                Id = StoreId,
                OrganizationId = OrganizationId,
                Name = "Demo Shopify Store",
                ShopifyDomain = "stockguard-demo.myshopify.com"
            });
        }

        if (!await context.Warehouses
            .AnyAsync(x => x.Id == WarehouseId))
        {
            context.Warehouses.Add(new Warehouse
            {
                Id = WarehouseId,
                OrganizationId = OrganizationId,
                Name = "Main Warehouse"
            });
        }

        await context.SaveChangesAsync();
    }
}
