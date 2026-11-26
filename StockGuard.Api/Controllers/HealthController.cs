using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StockGuard.Infrastructure.Data;

namespace StockGuard.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly StockGuardDbContext _context;
    private readonly ILogger<HealthController> _logger;

    public HealthController(
        StockGuardDbContext context,
        ILogger<HealthController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new
        {
            Application = "StockGuard Audit",
            Version = "1.1.0",
            Status = "Healthy",
            TimestampUtc = DateTime.UtcNow
        });
    }

    [HttpGet("database")]
    public async Task<IActionResult> GetDatabaseHealth(
        CancellationToken cancellationToken)
    {
        bool connected =
            await _context.Database.CanConnectAsync(
                cancellationToken);

        if (!connected)
        {
            _logger.LogWarning(
                "PostgreSQL health check failed.");

            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new
                {
                    Status = "Unhealthy",
                    Database = "Disconnected",
                    TimestampUtc = DateTime.UtcNow
                });
        }

        return Ok(new
        {
            Status = "Healthy",
            Database = "Connected",
            Provider = "PostgreSQL",
            TimestampUtc = DateTime.UtcNow
        });
    }
}
