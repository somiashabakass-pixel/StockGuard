using System.Text;
using StockGuard.Infrastructure.Services;
using Xunit;

namespace StockGuard.Tests;

public class CsvProcessingCancellationTests
{
    [Fact]
    public void ShopifyCsv_CancelledDuringRead_Throws()
    {
        using var source = new CancellationTokenSource();

        using var stream = new CancellingStream(
            Encoding.UTF8.GetBytes(
                "SKU,Location,On hand (current)\n" +
                "SKU-001,Main,100\n" +
                "SKU-002,Main,200\n"),
            source);

        CsvUploadService service = new();

        Assert.Throws<OperationCanceledException>(
            () => service.ReadShopifyInventory(
                stream, source.Token));

        Assert.True(source.IsCancellationRequested);
    }

    [Fact]
    public void WarehouseCsv_CancelledDuringRead_Throws()
    {
        using var source = new CancellationTokenSource();

        using var stream = new CancellingStream(
            Encoding.UTF8.GetBytes(
                "SKU,Quantity\n" +
                "SKU-001,100\n" +
                "SKU-002,200\n"),
            source);

        CsvUploadService service = new();

        Assert.Throws<OperationCanceledException>(
            () => service.ReadWarehouseInventory(
                stream, source.Token));

        Assert.True(source.IsCancellationRequested);
    }

    private sealed class CancellingStream : MemoryStream
    {
        private readonly CancellationTokenSource _source;

        public CancellingStream(
            byte[] data,
            CancellationTokenSource source)
            : base(data)
        {
            _source = source;
        }

        public override int Read(
            byte[] buffer,
            int offset,
            int count)
        {
            int bytesRead = base.Read(buffer, offset, count);

            if (bytesRead > 0)
            {
                _source.Cancel();
            }

            return bytesRead;
        }

        public override int Read(Span<byte> buffer)
        {
            int bytesRead = base.Read(buffer);

            if (bytesRead > 0)
            {
                _source.Cancel();
            }

            return bytesRead;
        }
    }
}
