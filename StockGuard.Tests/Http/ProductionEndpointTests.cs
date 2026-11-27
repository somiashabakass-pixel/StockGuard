using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
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

public class ProductionEndpointTests
{
    private static IWebHostEnvironment CreateEnvironment(
        string environmentName)
    {
        return new TestWebHostEnvironment
        {
            EnvironmentName = environmentName
        };
    }

    private static DbContextOptions<StockGuardDbContext>
        CreateDisconnectedOptions()
    {
        return new DbContextOptionsBuilder<StockGuardDbContext>()
            .Options;
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public async Task Save_IsBlocked(string environment)
    {
        var controller = new AuditsController(
            new InventoryAuditService(),
            new InventoryRiskService(),
            new AuditPersistenceService(
                CreateDisconnectedOptions()),
            CreateEnvironment(environment),
            NullLogger<AuditsController>.Instance);

        var result = await controller.Save(
            new AuditRequestDto());

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public async Task Upload_IsBlocked(string environment)
    {
        var controller = new CsvAuditsController(
            new CsvUploadService(),
            new InventoryAuditService(),
            new InventoryRiskService(),
            new AuditPersistenceService(
                CreateDisconnectedOptions()),
            CreateEnvironment(environment),
            NullLogger<CsvAuditsController>.Instance);

        var result = await controller.Upload(
            new CsvAuditRequestDto());

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public async Task History_IsBlocked(string environment)
    {
        var controller = new AuditHistoryController(
            new AuditHistoryService(
                CreateDisconnectedOptions()),
            CreateEnvironment(environment));

        var result = await controller.GetHistory();

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public async Task Results_IsBlocked(string environment)
    {
        var controller = new AuditHistoryController(
            new AuditHistoryService(
                CreateDisconnectedOptions()),
            CreateEnvironment(environment));

        var result = await controller.GetResults(
            Guid.NewGuid());

        Assert.IsType<NotFoundResult>(result.Result);
    }

    private sealed class TestWebHostEnvironment
        : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } =
            "Production";

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
