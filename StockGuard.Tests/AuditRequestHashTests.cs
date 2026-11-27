using StockGuard.Core.Models;
using StockGuard.Core.Services;

namespace StockGuard.Tests;

public class AuditRequestHashTests
{
    private static readonly Guid OrganizationId = Guid.NewGuid();
    private static readonly Guid StoreId = Guid.NewGuid();
    private static readonly Guid WarehouseId = Guid.NewGuid();

    private static List<InventoryRecord> CreateInventory()
    {
        return new()
        {
            new() { SKU = "SKU-001", Quantity = 100 },
            new() { SKU = "SKU-002", Quantity = 50 }
        };
    }

    private static string Compute(
        List<InventoryRecord> shopify,
        List<InventoryRecord> warehouse)
    {
        return AuditRequestHashService.ComputeHash(
            OrganizationId,
            StoreId,
            WarehouseId,
            shopify,
            warehouse);
    }

    [Fact]
    public void IdenticalRequests_ProduceSameHash()
    {
        Assert.Equal(
            Compute(CreateInventory(), CreateInventory()),
            Compute(CreateInventory(), CreateInventory()));
    }

    [Fact]
    public void DifferentRowOrder_ProducesSameHash()
    {
        var inventory = CreateInventory();
        inventory.Reverse();

        Assert.Equal(
            Compute(CreateInventory(), CreateInventory()),
            Compute(inventory, CreateInventory()));
    }

    [Fact]
    public void DifferentCapitalization_ProducesSameHash()
    {
        var inventory = CreateInventory();
        inventory[0].SKU = "sku-001";

        Assert.Equal(
            Compute(CreateInventory(), CreateInventory()),
            Compute(inventory, CreateInventory()));
    }

    [Fact]
    public void ExtraWhitespace_ProducesSameHash()
    {
        var inventory = CreateInventory();
        inventory[0].SKU = "  SKU-001  ";

        Assert.Equal(
            Compute(CreateInventory(), CreateInventory()),
            Compute(inventory, CreateInventory()));
    }

    [Fact]
    public void DifferentQuantity_ProducesDifferentHash()
    {
        var inventory = CreateInventory();
        inventory[0].Quantity = 99;

        Assert.NotEqual(
            Compute(CreateInventory(), CreateInventory()),
            Compute(inventory, CreateInventory()));
    }

    [Theory]
    [InlineData("Organization")]
    [InlineData("Store")]
    [InlineData("Warehouse")]
    public void DifferentScope_ProducesDifferentHash(
        string scope)
    {
        string original = Compute(
            CreateInventory(),
            CreateInventory());

        string changed =
            AuditRequestHashService.ComputeHash(
                scope == "Organization"
                    ? Guid.NewGuid() : OrganizationId,
                scope == "Store"
                    ? Guid.NewGuid() : StoreId,
                scope == "Warehouse"
                    ? Guid.NewGuid() : WarehouseId,
                CreateInventory(),
                CreateInventory());

        Assert.NotEqual(original, changed);
    }

    [Fact]
    public void SwappedInventorySources_ProduceDifferentHash()
    {
        var shopify = CreateInventory();
        var warehouse = CreateInventory();

        warehouse[0].Quantity = 80;

        Assert.NotEqual(
            Compute(shopify, warehouse),
            Compute(warehouse, shopify));
    }

    [Fact]
    public void Hash_HasValidSha256Format()
    {
        string hash = Compute(
            CreateInventory(),
            CreateInventory());

        Assert.Equal(64, hash.Length);
        Assert.Matches("^[0-9A-F]{64}$", hash);
    }
}
