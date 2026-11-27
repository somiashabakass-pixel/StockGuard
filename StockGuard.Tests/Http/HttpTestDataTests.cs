using Microsoft.EntityFrameworkCore;
using StockGuard.Infrastructure.Data;
using StockGuard.Tests.Integration;
using Xunit;

namespace StockGuard.Tests.Http;

[Trait("Category", "Integration")]
public class HttpTestDataTests
{
    [Fact]
    public async Task Initialize_ShouldCreateOrganization()
    {
        await HttpTestData.InitializeAsync();

        await using var context =
            TestDatabase.CreateContext();

        var organization =
            await context.Organizations.SingleAsync(
                x => x.Id ==
                    DevelopmentDataService.OrganizationId);

        Assert.True(organization.IsActive);
    }

    [Fact]
    public async Task Initialize_ShouldCreateStore()
    {
        await HttpTestData.InitializeAsync();

        await using var context =
            TestDatabase.CreateContext();

        var store =
            await context.Stores.SingleAsync(
                x => x.Id ==
                    DevelopmentDataService.StoreId);

        Assert.Equal(
            DevelopmentDataService.OrganizationId,
            store.OrganizationId);

        Assert.True(store.IsActive);
    }

    [Fact]
    public async Task Initialize_ShouldCreateWarehouse()
    {
        await HttpTestData.InitializeAsync();

        await using var context =
            TestDatabase.CreateContext();

        var warehouse =
            await context.Warehouses.SingleAsync(
                x => x.Id ==
                    DevelopmentDataService.WarehouseId);

        Assert.Equal(
            DevelopmentDataService.OrganizationId,
            warehouse.OrganizationId);

        Assert.True(warehouse.IsActive);
    }
}
