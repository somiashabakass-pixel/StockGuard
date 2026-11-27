using Microsoft.EntityFrameworkCore;
using Npgsql;
using Serilog;
using Serilog.Events;
using StockGuard.Core.Services;
using StockGuard.Infrastructure.Data;
using StockGuard.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

bool isTesting = builder.Environment.IsEnvironment("Testing");

builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration
        .MinimumLevel.Information()
        .MinimumLevel.Override(
            "Microsoft.AspNetCore",
            LogEventLevel.Warning)
        .MinimumLevel.Override(
            "Microsoft.EntityFrameworkCore",
            LogEventLevel.Warning)
        .Enrich.FromLogContext()
        .Enrich.WithProperty(
            "Application",
            "StockGuard Audit")
        .WriteTo.Console();

    if (!isTesting)
    {
        configuration.WriteTo.File(
            "Logs/stockguard-.log",
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 14,
            fileSizeLimitBytes: 10 * 1024 * 1024,
            rollOnFileSizeLimit: true);
    }
});

string connectionVariable = isTesting
    ? "STOCKGUARD_TEST_CONNECTION_STRING"
    : "STOCKGUARD_CONNECTION_STRING";

string connectionString =
    Environment.GetEnvironmentVariable(connectionVariable)
    ?? throw new InvalidOperationException(
        "StockGuard database connection is not configured.");

if (isTesting)
{
    var databaseBuilder =
        new NpgsqlConnectionStringBuilder(connectionString);

    if (databaseBuilder.Database != "stockguard_test")
    {
        throw new InvalidOperationException(
            "HTTP integration tests must use stockguard_test.");
    }
}

builder.Services.AddControllers();

builder.Services.AddProblemDetails();

builder.Services.AddOpenApi();

builder.Services.AddDbContext<StockGuardDbContext>(
    options => options.UseNpgsql(connectionString));

builder.Services.AddScoped<InventoryAuditService>();

builder.Services.AddScoped<InventoryRiskService>();

builder.Services.AddScoped<AuditPersistenceService>(sp =>
    new AuditPersistenceService(
        sp.GetRequiredService<DbContextOptions<StockGuardDbContext>>()));

builder.Services.AddScoped<AuditHistoryService>(sp =>
    new AuditHistoryService(
        sp.GetRequiredService<DbContextOptions<StockGuardDbContext>>()));

builder.Services.AddScoped<CsvUploadService>();

var app = builder.Build();

app.UseSerilogRequestLogging();

app.UseExceptionHandler(new ExceptionHandlerOptions
{
    StatusCodeSelector = exception =>
        exception is Microsoft.AspNetCore.Http.BadHttpRequestException badRequest
            ? badRequest.StatusCode
            : StatusCodes.Status500InternalServerError
});

app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint(
            "/openapi/v1.json",
            "StockGuard Audit API v1");

        options.RoutePrefix = "swagger";

        options.DocumentTitle =
            "StockGuard Audit API Documentation";
    });
}
else if (!isTesting)
{
    app.UseHttpsRedirection();
}

app.UseAuthorization();

app.MapControllers();

app.Logger.LogInformation(
    "StockGuard Audit API starting in {Environment} environment",
    app.Environment.EnvironmentName);

app.Run();

public partial class Program
{
}
