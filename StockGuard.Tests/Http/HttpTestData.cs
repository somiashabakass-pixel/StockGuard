using Microsoft.EntityFrameworkCore;
using StockGuard.Infrastructure.Data;
using StockGuard.Infrastructure.Data.Entities;
using StockGuard.Tests.Integration;

namespace StockGuard.Tests.Http;

public static class HttpTestData
{
    private static readonly SemaphoreSlim InitializationLock =
        new(1, 1);

    public static async Task InitializeAsync()
    {
        await InitializationLock.WaitAsync();

        try
        {
            await using var context =
                TestDatabase.CreateContext();

            if (!await context.Organizations.AnyAsync(
                x => x.Id == DevelopmentDataService.OrganizationId))
            {
                context.Organizations.Add(new Organization
                {
                    Id = DevelopmentDataService.OrganizationId,
                    Name = "StockGuard HTTP Test Company"
                });
            }

            if (!await context.Stores.AnyAsync(
                x => x.Id == DevelopmentDataService.StoreId))
            {
                context.Stores.Add(new Store
                {
                    Id = DevelopmentDataService.StoreId,
                    OrganizationId =
                        DevelopmentDataService.OrganizationId,
                    Name = "HTTP Test Shopify Store",
                    ShopifyDomain =
                        "stockguard-http-test.myshopify.com"
                });
            }

            if (!await context.Warehouses.AnyAsync(
                x => x.Id == DevelopmentDataService.WarehouseId))
            {
                context.Warehouses.Add(new Warehouse
                {
                    Id = DevelopmentDataService.WarehouseId,
                    OrganizationId =
                        DevelopmentDataService.OrganizationId,
                    Name = "HTTP Test Warehouse"
                });
            }

            await context.SaveChangesAsync();
        }
        finally
        {
            InitializationLock.Release();
        }
    }
}
