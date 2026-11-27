using StockGuard.Api.Services.Auth;
using Microsoft.AspNetCore.Identity;
using StockGuard.Infrastructure.Data.Entities;
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

builder.Services.AddIdentityCore<ApplicationUser>(options =>
{
    options.User.RequireUniqueEmail = true;

    options.Password.RequiredLength = 15;
    options.Password.RequiredUniqueChars = 1;
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;

    options.Lockout.AllowedForNewUsers = true;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan =
        TimeSpan.FromMinutes(15);

    options.SignIn.RequireConfirmedEmail = true;
})
.AddEntityFrameworkStores<StockGuardDbContext>()
    .AddSignInManager();

builder.Services.Configure<PasswordHasherOptions>(options =>
{
    options.CompatibilityMode =
        PasswordHasherCompatibilityMode.IdentityV3;

    options.IterationCount = 210_000;
});

builder.Services.AddScoped<RegistrationService>();
builder.Services.AddSingleton<LoginTimingProtection>();
builder.Services.AddSingleton<ILoginTimingProtection>(
    services => services.GetRequiredService<LoginTimingProtection>());
builder.Services.AddScoped<LoginService>();

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

// Initialize login timing protection before accepting requests.
_ = app.Services.GetRequiredService<ILoginTimingProtection>();

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
