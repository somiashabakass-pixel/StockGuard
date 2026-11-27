using System.Net;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using StockGuard.Api.Controllers;
using StockGuard.Api.Services.Auth;
using Xunit;

namespace StockGuard.Tests.Http;

public sealed class RegistrationEnvironmentTests
{
    [Theory]
    [InlineData("Production", "Valid")]
    [InlineData("Production", "Invalid")]
    [InlineData("Production", "Malformed")]
    [InlineData("Production", "Empty")]
    [InlineData("Staging", "Valid")]
    [InlineData("Staging", "Invalid")]
    [InlineData("Staging", "Malformed")]
    [InlineData("Staging", "Empty")]
    public async Task Registration_IsBlockedBeforeModelBinding(
        string environment,
        string requestType)
    {
        int serviceResolutionAttempts = 0;

        var builder = WebApplication.CreateBuilder(
            new WebApplicationOptions
            {
                EnvironmentName = environment
            });

        builder.WebHost.UseTestServer();

        builder.Services
            .AddControllers()
            .AddApplicationPart(
                typeof(AuthController).Assembly);

        builder.Services.AddScoped<RegistrationService>(_ =>
        {
            Interlocked.Increment(
                ref serviceResolutionAttempts);

            throw new InvalidOperationException(
                "Registration service must not be resolved.");
        });

        await using var app = builder.Build();

        app.MapControllers();

        await app.StartAsync();

        using var client = app.GetTestClient();

        string json = requestType switch
        {
            "Valid" => """
                {
                    "firstName": "Security",
                    "lastName": "Test",
                    "organizationName": "Blocked Organization",
                    "email": "blocked@example.test",
                    "password": "secure test passphrase",
                    "confirmPassword": "secure test passphrase"
                }
                """,

            "Invalid" => """
                {
                    "email": "invalid-email",
                    "password": "short"
                }
                """,

            "Malformed" => "{\"firstName\":",

            "Empty" => "",

            _ => throw new ArgumentOutOfRangeException(
                nameof(requestType))
        };

        using var content = new StringContent(
            json,
            Encoding.UTF8,
            "application/json");

        using var response = await client.PostAsync(
            "/api/auth/register",
            content);

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);

        Assert.Equal(
            0,
            Volatile.Read(ref serviceResolutionAttempts));

        await app.StopAsync();
    }
}