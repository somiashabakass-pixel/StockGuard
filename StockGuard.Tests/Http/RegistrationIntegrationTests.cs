using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StockGuard.Api.Dtos.Auth;
using StockGuard.Api.Services.Auth;
using StockGuard.Infrastructure.Data;
using StockGuard.Infrastructure.Data.Entities;
using Xunit;

namespace StockGuard.Tests.Http;

public sealed class RegistrationIntegrationTests
    : IClassFixture<StockGuardApiFactory>
{
    private readonly StockGuardApiFactory _factory;

    private const string TestPassword =
        "a secure registration test passphrase";

    public RegistrationIntegrationTests(
        StockGuardApiFactory factory)
    {
        _factory = factory;
    }

    private static RegisterRequest CreateRequest()
    {
        var identifier = Guid.NewGuid().ToString("N");

        return new RegisterRequest
        {
            FirstName = "  Integration  ",
            LastName = "  Customer  ",
            OrganizationName =
                $"  Registration Test {identifier}  ",
            Email = $"  {identifier}@example.test  ",
            Password = TestPassword,
            ConfirmPassword = TestPassword
        };
    }

    private static void VerifyTestDatabase(
        StockGuardDbContext context)
    {
        if (context.Database.GetDbConnection().Database
            != "stockguard_test")
        {
            throw new InvalidOperationException(
                "Registration tests require stockguard_test.");
        }
    }

    private async Task CleanupAsync(RegisterRequest request, string? originalOrganizationName = null)
    {
        using var scope =
            _factory.Services.CreateScope();

        var context = scope.ServiceProvider
            .GetRequiredService<StockGuardDbContext>();

        VerifyTestDatabase(context);

        string email = request.Email
            .Trim()
            .ToUpperInvariant();

        string organizationName =
            (originalOrganizationName ?? request.OrganizationName).Trim();

        await using var transaction =
            await context.Database.BeginTransactionAsync();

        var users = await context.Users
            .Where(x => x.NormalizedEmail == email)
            .ToListAsync();

        var organizations = await context.Organizations
            .Where(x => x.Name == organizationName)
            .ToListAsync();

        var userIds = users
            .Select(x => x.Id)
            .ToList();

        var organizationIds = organizations
            .Select(x => x.Id)
            .ToList();

        var memberships =
            await context.OrganizationMemberships
                .Where(x =>
                    userIds.Contains(x.UserId) ||
                    organizationIds.Contains(x.OrganizationId))
                .ToListAsync();

        context.OrganizationMemberships
            .RemoveRange(memberships);

        await context.SaveChangesAsync();

        context.Users.RemoveRange(users);

        context.Organizations.RemoveRange(organizations);

        await context.SaveChangesAsync();

        await transaction.CommitAsync();
    }

    [Fact]
    public void RegistrationService_CanBeResolved()
    {
        using var scope =
            _factory.Services.CreateScope();

        var service = scope.ServiceProvider
            .GetRequiredService<RegistrationService>();

        Assert.NotNull(service);
    }

    [Fact]
    public async Task ValidRegistration_CreatesCompleteAccount()
    {
        var request = CreateRequest();

        try
        {
            using var scope =
                _factory.Services.CreateScope();

            var context = scope.ServiceProvider
                .GetRequiredService<StockGuardDbContext>();

            VerifyTestDatabase(context);

            var service = scope.ServiceProvider
                .GetRequiredService<RegistrationService>();

            var result = await service.RegisterAsync(request);

            Assert.True(result.Succeeded, result.ErrorCode);
            Assert.Null(result.ErrorCode);
            Assert.NotNull(result.Account);

            var account = result.Account!;

            context.ChangeTracker.Clear();

            var user = await context.Users
                .AsNoTracking()
                .SingleAsync(x => x.Id == account.UserId);

            var organization =
                await context.Organizations
                    .AsNoTracking()
                    .SingleAsync(
                        x => x.Id == account.OrganizationId);

            var membership =
                await context.OrganizationMemberships
                    .AsNoTracking()
                    .SingleAsync(
                        x => x.UserId == account.UserId &&
                             x.OrganizationId ==
                                 account.OrganizationId);

            Assert.Equal("Integration", user.FirstName);
            Assert.Equal("Customer", user.LastName);

            Assert.Equal(
                request.Email.Trim(),
                user.Email);

            Assert.Equal(
                request.Email.Trim().ToUpperInvariant(),
                user.NormalizedEmail);

            Assert.Equal(
                request.OrganizationName.Trim(),
                organization.Name);

            Assert.True(user.IsActive);
            Assert.True(organization.IsActive);
            Assert.True(membership.IsActive);

            Assert.False(user.EmailConfirmed);

            Assert.Equal(
                OrganizationRole.Owner,
                membership.Role);

            Assert.Equal("Owner", account.Role);

            Assert.True(
                account.EmailConfirmationRequired);

            Assert.NotEmpty(user.PasswordHash!);

            Assert.NotEqual(
                TestPassword,
                user.PasswordHash);

            var hasher = scope.ServiceProvider
                .GetRequiredService<
                    IPasswordHasher<ApplicationUser>>();

            Assert.Equal(
                PasswordVerificationResult.Success,
                hasher.VerifyHashedPassword(
                    user,
                    user.PasswordHash!,
                    TestPassword));

            Assert.Equal(
                PasswordVerificationResult.Failed,
                hasher.VerifyHashedPassword(
                    user,
                    user.PasswordHash!,
                    "incorrect password"));

            Assert.Equal(
                1,
                await context.OrganizationMemberships
                    .CountAsync(x => x.UserId == user.Id));
        }
        finally
        {
            await CleanupAsync(request);
        }
    }

    [Theory]
    [InlineData("ShortPassword")]
    [InlineData("PasswordMismatch")]
    [InlineData("InvalidEmail")]
    [InlineData("EmptyFirstName")]
    [InlineData("EmptyOrganization")]
    [InlineData("LongPassword")]
    public async Task InvalidRegistration_CreatesNoRecords(
        string invalidField)
    {
        var original = CreateRequest();

        var request = new RegisterRequest
        {
            FirstName = invalidField == "EmptyFirstName"
                ? "   "
                : original.FirstName,

            LastName = original.LastName,

            OrganizationName =
                invalidField == "EmptyOrganization"
                    ? "   "
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
                "PasswordMismatch" => "different passphrase",
                "ShortPassword" => "short",
                "LongPassword" => new string('a', 129),
                _ => original.ConfirmPassword
            }
        };

        try
        {
            using var scope =
                _factory.Services.CreateScope();

            var context = scope.ServiceProvider
                .GetRequiredService<StockGuardDbContext>();

            VerifyTestDatabase(context);

            var service = scope.ServiceProvider
                .GetRequiredService<RegistrationService>();

            var result = await service.RegisterAsync(request);

            Assert.False(result.Succeeded);
            Assert.Null(result.Account);

            Assert.Equal(
                "InvalidRegistration",
                result.ErrorCode);

            string normalizedEmail =
                request.Email.Trim().ToUpperInvariant();

            Assert.False(
                await context.Users.AnyAsync(
                    x => x.NormalizedEmail ==
                        normalizedEmail));

            Assert.False(
                await context.Organizations.AnyAsync(
                    x => x.Name ==
                        request.OrganizationName.Trim()));
        }
        finally
        {
            await CleanupAsync(request, original.OrganizationName);
        }
    }

    [Fact]
    public async Task CancelledRegistration_CreatesNoRecords()
    {
        var request = CreateRequest();

        try
        {
            using var scope =
                _factory.Services.CreateScope();

            var context = scope.ServiceProvider
                .GetRequiredService<StockGuardDbContext>();

            VerifyTestDatabase(context);

            var service = scope.ServiceProvider
                .GetRequiredService<RegistrationService>();

            using var cancellation =
                new CancellationTokenSource();

            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<
                OperationCanceledException>(
                    () => service.RegisterAsync(
                        request,
                        cancellation.Token));

            string email =
                request.Email.Trim().ToUpperInvariant();

            Assert.False(
                await context.Users.AnyAsync(
                    x => x.NormalizedEmail == email));

            Assert.False(
                await context.Organizations.AnyAsync(
                    x => x.Name ==
                        request.OrganizationName.Trim()));
        }
        finally
        {
            await CleanupAsync(request);
        }
    }
}