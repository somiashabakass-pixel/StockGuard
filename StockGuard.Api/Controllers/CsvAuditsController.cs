using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using StockGuard.Api.Dtos;
using StockGuard.Api.Helpers;
using StockGuard.Core.Models;
using StockGuard.Core.Services;
using StockGuard.Infrastructure.Services;
using StockGuard.Infrastructure.Data;

namespace StockGuard.Api.Controllers;

[ApiController]
[Route("api/audits")]
public class CsvAuditsController : ControllerBase
{
    private readonly CsvUploadService _csvService;
    private readonly InventoryAuditService _auditService;
    private readonly InventoryRiskService _riskService;
    private readonly AuditPersistenceService _persistenceService;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<CsvAuditsController> _logger;

    public CsvAuditsController(
        CsvUploadService csvService,
        InventoryAuditService auditService,
        InventoryRiskService riskService,
        AuditPersistenceService persistenceService,
        IWebHostEnvironment environment,
        ILogger<CsvAuditsController> logger)
    {
        _csvService = csvService;
        _auditService = auditService;
        _riskService = riskService;
        _persistenceService = persistenceService;
        _environment = environment;
        _logger = logger;
    }

    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(12 * 1024 * 1024)]
    [RequestFormLimits(
        MultipartBodyLengthLimit = 5 * 1024 * 1024)]
    public async Task<ActionResult<AuditResponseDto>> Upload(
        [FromForm] CsvAuditRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!_environment.IsDevelopment() && !_environment.IsEnvironment("Testing"))
        {
            return NotFound();
        }

        cancellationToken.ThrowIfCancellationRequested();

        var stopwatch = Stopwatch.StartNew();

        if (!IdempotencyHeader.TryRead(
            Request.Headers,
            out string? idempotencyKey,
            out string? headerError))
        {
            ModelState.AddModelError(
                IdempotencyHeader.HeaderName,
                headerError!);

            return ValidationProblem(ModelState);
        }

        DateTime startedAtUtc = DateTime.UtcNow;

        ValidateFile(
            request.ShopifyFile,
            nameof(request.ShopifyFile));

        ValidateFile(
            request.WarehouseFile,
            nameof(request.WarehouseFile));

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        List<InventoryRecord> shopifyInventory;
        List<InventoryRecord> warehouseInventory;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var shopifyStream =
                request.ShopifyFile.OpenReadStream();

            shopifyInventory =
                _csvService.ReadShopifyInventory(shopifyStream, cancellationToken);
        }
        catch (InvalidDataException exception)
        {
            ModelState.AddModelError(
                nameof(request.ShopifyFile),
                exception.Message);

            return ValidationProblem(ModelState);
        }

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            using var warehouseStream =
                request.WarehouseFile.OpenReadStream();

            warehouseInventory =
                _csvService.ReadWarehouseInventory(warehouseStream, cancellationToken);
        }
        catch (InvalidDataException exception)
        {
            ModelState.AddModelError(
                nameof(request.WarehouseFile),
                exception.Message);

            return ValidationProblem(ModelState);
        }

        cancellationToken.ThrowIfCancellationRequested();

        string? requestHash = idempotencyKey is null
            ? null
            : AuditRequestHashService.ComputeHash(
                DevelopmentDataService.OrganizationId,
                DevelopmentDataService.StoreId,
                DevelopmentDataService.WarehouseId,
                shopifyInventory,
                warehouseInventory);

        cancellationToken.ThrowIfCancellationRequested();

        if (idempotencyKey is not null)
        {
            try
            {
                var existing =
                    await _persistenceService.TryGetExistingAuditAsync(
                        DevelopmentDataService.OrganizationId,
                        DevelopmentDataService.StoreId,
                        DevelopmentDataService.WarehouseId,
                        idempotencyKey,
                        requestHash!,
                        cancellationToken);

                if (existing is not null)
                {
                    stopwatch.Stop();

                    _logger.LogInformation(
                        "CSV audit {AuditId} replayed. DurationMs: {DurationMs}",
                        existing.AuditId,
                        stopwatch.Elapsed.TotalMilliseconds);

                    return Ok(
                        AuditResponseMapper.FromPersistedSnapshot(
                            existing));
                }
            }
            catch (IdempotencyConflictException)
            {
                return Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    title: "Idempotency conflict",
                    detail: "The idempotency key has already been used with a different audit request.");
            }
        }

        List<AuditResult> results =
            _auditService.CompareInventory(shopifyInventory, warehouseInventory, cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        long totalDiscrepancy = results
            .Where(x => x.Status == "Mismatch")
            .Sum(x => Math.Abs(
                (long)(x.Difference ?? 0)));

        if (totalDiscrepancy > int.MaxValue)
        {
            ModelState.AddModelError(
                "Inventory",
                "Total discrepancy exceeds the supported limit.");

            return ValidationProblem(ModelState);
        }

        InventoryRiskSummary risk =
            _riskService.CalculateRisk(results, cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();



        cancellationToken.ThrowIfCancellationRequested();

        AuditSaveResult savedAudit;

        try
        {
            savedAudit =
                await _persistenceService.SaveAuditWithDetailsAsync(
                    DevelopmentDataService.OrganizationId,
                    DevelopmentDataService.StoreId,
                    DevelopmentDataService.WarehouseId,
                    startedAtUtc,
                    results,
                    risk,
                    cancellationToken,
                    idempotencyKey: idempotencyKey,
                    requestHash: requestHash);
        }
        catch (IdempotencyConflictException)
        {
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Idempotency conflict",
                detail: "The idempotency key has already been used with a different audit request.");
        }

        PersistedAuditSnapshot snapshot =
            await _persistenceService.GetPersistedAuditAsync(
                DevelopmentDataService.OrganizationId,
                savedAudit.AuditId,
                cancellationToken)
            ?? throw new InvalidOperationException(
                "The saved audit could not be retrieved.");

        AuditResponseDto response =
            AuditResponseMapper.FromPersistedSnapshot(
                snapshot);

        stopwatch.Stop();

        _logger.LogInformation(
            "CSV audit {AuditId} completed. Products: {TotalProducts}, Matching: {MatchingProducts}, Mismatched: {MismatchedProducts}, Missing: {MissingProducts}, Discrepancy: {TotalUnitsDiscrepancy}, DurationMs: {DurationMs}",
            response.AuditId,
            response.TotalProducts,
            response.MatchingProducts,
            response.MismatchedProducts,
            response.MissingProducts,
            response.TotalUnitsDiscrepancy,
            stopwatch.Elapsed.TotalMilliseconds);

        return Ok(response);
    }

    private void ValidateFile(
        IFormFile? file,
        string fieldName)
    {
        if (file is null || file.Length == 0)
        {
            ModelState.AddModelError(
                fieldName,
                "A nonempty CSV file is required.");

            return;
        }

        if (file.Length > CsvUploadService.MaxFileSize)
        {
            ModelState.AddModelError(
                fieldName,
                "The file exceeds the 5 MB limit.");
        }

        if (!string.Equals(
            Path.GetExtension(file.FileName),
            ".csv",
            StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(
                fieldName,
                "Only CSV files are supported.");
        }
    }
}
