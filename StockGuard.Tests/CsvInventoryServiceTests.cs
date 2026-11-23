
using StockGuard.Infrastructure.Services;
using Xunit;

namespace StockGuard.Tests;

public class CsvInventoryServiceTests
{
    [Fact]
    public void LoadInventory_RejectsInvalidQuantity()
    {
        // ARRANGE
        string fileName = $"test-{Guid.NewGuid()}.csv";
        string filePath = Path.Combine(
            AppContext.BaseDirectory, fileName);

        try
        {
            File.WriteAllLines(filePath, new[]
            {
                "SKU,Quantity",
                "SKU-001,abc"
            });

            CsvInventoryService service = new();

            // ACT + ASSERT
            Assert.Throws<FormatException>(() =>
                service.LoadInventory(fileName));
        }
        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    [Fact]
    public void LoadInventory_RejectsNegativeQuantity()
    {
        string fileName = $"test-{Guid.NewGuid()}.csv";
        string filePath = Path.Combine(
            AppContext.BaseDirectory, fileName);

        try
        {
            File.WriteAllLines(filePath, new[]
            {
                "SKU,Quantity",
                "SKU-001,-10"
            });

            CsvInventoryService service = new();

            Assert.Throws<FormatException>(() =>
                service.LoadInventory(fileName));
        }
        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    [Fact]
    public void LoadInventory_RejectsDuplicateSku()
    {
        string fileName = $"test-{Guid.NewGuid()}.csv";
        string filePath = Path.Combine(
            AppContext.BaseDirectory, fileName);

        try
        {
            File.WriteAllLines(filePath, new[]
            {
                "SKU,Quantity",
                "SKU-001,100",
                "SKU-001,50"
            });

            CsvInventoryService service = new();

            Assert.Throws<FormatException>(() =>
                service.LoadInventory(fileName));
        }
        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

[Fact]
public void LoadInventory_RejectsEmptyFile()
{
    string fileName = $"test-{Guid.NewGuid()}.csv";
    string filePath = Path.Combine(
        AppContext.BaseDirectory, fileName);

    try
    {
        File.WriteAllText(filePath, "");

        CsvInventoryService service = new();

        Assert.Throws<FormatException>(() =>
            service.LoadInventory(fileName));
    }
    finally
    {
        if (File.Exists(filePath))
            File.Delete(filePath);
    }
}

[Fact]
public void LoadInventory_RejectsInvalidHeader()
{
    string fileName = $"test-{Guid.NewGuid()}.csv";
    string filePath = Path.Combine(
        AppContext.BaseDirectory, fileName);

    try
    {
        File.WriteAllLines(filePath, new[]
        {
            "Product,Price",
            "SKU-001,100"
        });

        CsvInventoryService service = new();

        Assert.Throws<FormatException>(() =>
            service.LoadInventory(fileName));
    }
    finally
    {
        if (File.Exists(filePath))
            File.Delete(filePath);
    }
}

}

