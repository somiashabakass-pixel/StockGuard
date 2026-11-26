using Microsoft.EntityFrameworkCore;
using StockGuard.Core.Services;
using StockGuard.Infrastructure.Data;
using StockGuard.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

string connectionString =
    Environment.GetEnvironmentVariable(
        "STOCKGUARD_CONNECTION_STRING")
    ?? throw new InvalidOperationException(
        "StockGuard database connection is not configured.");

builder.Services.AddControllers();

builder.Services.AddOpenApi();

builder.Services.AddDbContext<StockGuardDbContext>(
    options => options.UseNpgsql(connectionString));

builder.Services.AddScoped<InventoryAuditService>();

builder.Services.AddScoped<InventoryRiskService>();

builder.Services.AddScoped<AuditPersistenceService>();

builder.Services.AddScoped<AuditHistoryService>();

builder.Services.AddScoped<CsvUploadService>();

var app = builder.Build();

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
else
{
    app.UseHttpsRedirection();
}

app.UseAuthorization();

app.MapControllers();

app.Run();
