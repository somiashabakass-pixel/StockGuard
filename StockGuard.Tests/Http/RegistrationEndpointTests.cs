using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StockGuard.Api.Dtos.Auth;
using StockGuard.Infrastructure.Data;
using Xunit;

namespace StockGuard.Tests.Http;

public sealed class RegistrationEndpointTests
    : IClassFixture<StockGuardApiFactory>
{
    private readonly StockGuardApiFactory _factory;

    private const string Endpoint = "/api/auth/register";

    private const string Password =
        "secure endpoint test passphrase";

    public RegistrationEndpointTests(
        StockGuardApiFactory factory)
    {
        _factory = factory;
    }

    private static RegisterRequest CreateRequest()
    {
        var identifier = Guid.NewGuid().ToString("N");

        return new RegisterRequest
        {
            FirstName = "Endpoint",
            LastName = "Test",
            OrganizationName = $"Endpoint Test {identifier}",
            Email = $"{identifier}@example.test",
            Password = Password,
            ConfirmPassword = Password
        };
    }

    private static void VerifyDatabase(
        StockGuardDbContext context)
    {
        if (context.Database.GetDbConnection().Database
            != "stockguard_test")
        {
            throw new InvalidOperationException(
                "Registration endpoint tests require stockguard_test.");
        }
    }

    private async Task CleanupAsync(RegisterRequest request)
    {
        using var scope =
            _factory.Services.CreateScope();

        var context = scope.ServiceProvider
            .GetRequiredService<StockGuardDbContext>();

        VerifyDatabase(context);

        await using var transaction =
            await context.Database.BeginTransactionAsync();

        var userIds = await context.Users
            .Where(x => x.NormalizedEmail ==
                request.Email.ToUpperInvariant())
            .Select(x => x.Id)
            .ToListAsync();

        var organizationIds = await context.Organizations
            .Where(x => x.Name == request.OrganizationName)
            .Select(x => x.Id)
            .ToListAsync();

        await context.OrganizationMemberships
            .Where(x =>
                userIds.Contains(x.UserId) ||
                organizationIds.Contains(x.OrganizationId))
            .ExecuteDeleteAsync();

        await context.Users
            .Where(x => userIds.Contains(x.Id))
            .ExecuteDeleteAsync();

        await context.Organizations
            .Where(x => organizationIds.Contains(x.Id))
            .ExecuteDeleteAsync();

        await transaction.CommitAsync();
    }

    [Fact]
    public async Task ValidAndDuplicateRegistration_ReturnIdenticalResponses()
    {
        var request = CreateRequest();

        try
        {
            using var client = _factory.CreateClient();

            using var first =
                await client.PostAsJsonAsync(
                    Endpoint, request);

            using var duplicate =
                await client.PostAsJsonAsync(
                    Endpoint, request);

            Assert.Equal(
                HttpStatusCode.Accepted,
                first.StatusCode);

            Assert.Equal(
                HttpStatusCode.Accepted,
                duplicate.StatusCode);

            string firstBody =
                await first.Content.ReadAsStringAsync();

            string duplicateBody =
                await duplicate.Content.ReadAsStringAsync();

            Assert.Equal(firstBody, duplicateBody);

            Assert.Equal(
                "{\"message\":\"Registration request received.\"}",
                firstBody);

            Assert.DoesNotContain(
                Password, firstBody);

            Assert.DoesNotContain(
                request.Email, firstBody);

            Assert.True(
                first.Headers.CacheControl?.NoStore == true);

            Assert.True(
                duplicate.Headers.CacheControl?.NoStore == true);

            using var scope =
                _factory.Services.CreateScope();

            var context = scope.ServiceProvider
                .GetRequiredService<StockGuardDbContext>();

            VerifyDatabase(context);

            var user = await context.Users
                .AsNoTracking()
                .SingleAsync(x => x.NormalizedEmail ==
                    request.Email.ToUpperInvariant());

            var organization = await context.Organizations
                .AsNoTracking()
                .SingleAsync(x => x.Name ==
                    request.OrganizationName);

            Assert.Equal(
                1,
                await context.OrganizationMemberships
                    .CountAsync(x =>
                        x.UserId == user.Id &&
                        x.OrganizationId == organization.Id));

            Assert.DoesNotContain(
                user.Id.ToString(), firstBody);

            Assert.DoesNotContain(
                organization.Id.ToString(), firstBody);

            Assert.DoesNotContain(
                user.PasswordHash!, firstBody);
        }
        finally
        {
            await CleanupAsync(request);
        }
    }

    [Theory]
    [InlineData("InvalidEmail")]
    [InlineData("ShortPassword")]
    [InlineData("PasswordMismatch")]
    [InlineData("EmptyOrganization")]
    [InlineData("LongPassword")]
    public async Task InvalidRegistration_ReturnsBadRequest(
        string invalidField)
    {
        var original = CreateRequest();

        var request = new RegisterRequest
        {
            FirstName = original.FirstName,
            LastName = original.LastName,

            OrganizationName =
                invalidField == "EmptyOrganization"
                    ? ""
                    : original.OrganizationName,

            Email = invalidField == "InvalidEmail"
                ? original.Email.Replace("@", "-")
                : original.Email,

            Password = invalidField switch
            {
                "ShortPassword" => "short",
                "LongPassword" => new string('a', 129),
                _ => original.Password
            },

            ConfirmPassword = invalidField switch
            {
                "ShortPassword" => "short",
                "LongPassword" => new string('a', 129),
                "PasswordMismatch" => "different test passphrase",
                _ => original.ConfirmPassword
            }
        };

        try
        {
            using var client = _factory.CreateClient();

            using var response =
                await client.PostAsJsonAsync(
                    Endpoint, request);

            Assert.Equal(
                HttpStatusCode.BadRequest,
                response.StatusCode);

            string body =
                await response.Content.ReadAsStringAsync();

            Assert.DoesNotContain(
                request.Password, body);

            Assert.DoesNotContain(
                request.ConfirmPassword, body);

            using var scope =
                _factory.Services.CreateScope();

            var context = scope.ServiceProvider
                .GetRequiredService<StockGuardDbContext>();

            VerifyDatabase(context);

            Assert.False(
                await context.Users.AnyAsync(
                    x => x.NormalizedEmail ==
                        request.Email.ToUpperInvariant()));

            Assert.False(
                await context.Organizations.AnyAsync(
                    x => x.Name == original.OrganizationName));
        }
        finally
        {
            await CleanupAsync(request);
        }
    }

    [Fact]
    public async Task MalformedJson_ReturnsBadRequest()
    {
        using var client = _factory.CreateClient();

        using var content = new StringContent(
            "{\"firstName\":",
            Encoding.UTF8,
            "application/json");

        using var response =
            await client.PostAsync(Endpoint, content);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }

    [Fact]
    public async Task EmptyRequest_ReturnsBadRequest()
    {
        using var client = _factory.CreateClient();

        using var content = new StringContent(
            "",
            Encoding.UTF8,
            "application/json");

        using var response =
            await client.PostAsync(Endpoint, content);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }
}