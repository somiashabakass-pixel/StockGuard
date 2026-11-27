using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using StockGuard.Api.Controllers;
using StockGuard.Api.Dtos;
using StockGuard.Core.Services;
using StockGuard.Infrastructure.Data;
using StockGuard.Infrastructure.Services;
using Xunit;

namespace StockGuard.Tests.Http;

public class ControllerCancellationTests
{
    private static DbContextOptions<StockGuardDbContext>
        CreateDisconnectedOptions()
    {
        return new DbContextOptionsBuilder<StockGuardDbContext>()
            .Options;
    }

    [Fact]
    public async Task Save_CancelledToken_StopsBeforePersistence()
    {
        var controller = new AuditsController(
            new InventoryAuditService(),
            new InventoryRiskService(),
            new AuditPersistenceService(
                CreateDisconnectedOptions()),
            new TestingEnvironment(),
            NullLogger<AuditsController>.Instance);

        using var cancellation =
            new CancellationTokenSource();

        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => controller.Save(
                new AuditRequestDto(),
                cancellation.Token));
    }

    [Fact]
    public async Task Upload_CancelledToken_StopsBeforePersistence()
    {
        var controller = new CsvAuditsController(
            new CsvUploadService(),
            new InventoryAuditService(),
            new InventoryRiskService(),
            new AuditPersistenceService(
                CreateDisconnectedOptions()),
            new TestingEnvironment(),
            NullLogger<CsvAuditsController>.Instance);

        using var cancellation =
            new CancellationTokenSource();

        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => controller.Upload(
                new CsvAuditRequestDto(),
                cancellation.Token));
    }

    [Fact]
    public async Task History_CancelledToken_StopsBeforeQuery()
    {
        var controller = new AuditHistoryController(
            new AuditHistoryService(
                CreateDisconnectedOptions()),
            new TestingEnvironment());

        using var cancellation =
            new CancellationTokenSource();

        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => controller.GetHistory(
                50,
                cancellation.Token));
    }

    [Fact]
    public async Task Results_CancelledToken_StopsBeforeQuery()
    {
        var controller = new AuditHistoryController(
            new AuditHistoryService(
                CreateDisconnectedOptions()),
            new TestingEnvironment());

        using var cancellation =
            new CancellationTokenSource();

        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => controller.GetResults(
                Guid.NewGuid(),
                cancellation.Token));
    }

    private sealed class TestingEnvironment
        : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } =
            "Testing";

        public string ApplicationName { get; set; } =
            "StockGuard.Api";

        public string ContentRootPath { get; set; } =
            Directory.GetCurrentDirectory();

        public IFileProvider ContentRootFileProvider
            { get; set; } = new NullFileProvider();

        public string WebRootPath { get; set; } =
            Directory.GetCurrentDirectory();

        public IFileProvider WebRootFileProvider
            { get; set; } = new NullFileProvider();
    }
}
