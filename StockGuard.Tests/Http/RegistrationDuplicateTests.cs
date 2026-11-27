using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using StockGuard.Api.Dtos.Auth;
using StockGuard.Api.Services.Auth;
using StockGuard.Infrastructure.Data;
using StockGuard.Infrastructure.Data.Entities;
using Xunit;

namespace StockGuard.Tests.Http;

public sealed class RegistrationDuplicateTests
    : IClassFixture<StockGuardApiFactory>
{
    private readonly StockGuardApiFactory _factory;

    public RegistrationDuplicateTests(
        StockGuardApiFactory factory)
    {
        _factory = factory;
    }

    private sealed class ConcurrentRegistrationInterceptor
        : SaveChangesInterceptor
    {
        private readonly TaskCompletionSource<bool> _gate =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private int _arrivals;

        public int Arrivals =>
            Volatile.Read(ref _arrivals);

        public override async ValueTask<InterceptionResult<int>>
            SavingChangesAsync(
                DbContextEventData eventData,
                InterceptionResult<int> result,
                CancellationToken cancellationToken = default)
        {
            bool isUserInsert =
                eventData.Context?.ChangeTracker
                    .Entries<ApplicationUser>()
                    .Any(x => x.State == EntityState.Added)
                == true;

            if (!isUserInsert)
            {
                return result;
            }

            if (Interlocked.Increment(ref _arrivals) == 2)
            {
                _gate.TrySetResult(true);
            }

            await _gate.Task.WaitAsync(
                TimeSpan.FromSeconds(15),
                cancellationToken);

            return result;
        }
    }

    private static RegisterRequest CreateRequest(
        string email,
        string organizationName)
    {
        return new RegisterRequest
        {
            FirstName = "Duplicate",
            LastName = "Test",
            Email = email,
            OrganizationName = organizationName,
            Password = "secure duplicate test passphrase",
            ConfirmPassword = "secure duplicate test passphrase"
        };
    }

    private static void VerifyTestDatabase(
        StockGuardDbContext context)
    {
        if (context.Database.GetDbConnection().Database
            != "stockguard_test")
        {
            throw new InvalidOperationException(
                "Duplicate tests require stockguard_test.");
        }
    }

    private static async Task<RegistrationResult> RegisterAsync(
        IServiceProvider services,
        RegisterRequest request)
    {
        using var scope = services.CreateScope();

        var context = scope.ServiceProvider
            .GetRequiredService<StockGuardDbContext>();

        VerifyTestDatabase(context);

        var service = scope.ServiceProvider
            .GetRequiredService<RegistrationService>();

        return await service.RegisterAsync(request);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DuplicateEmail_CreatesOnlyOneCompleteAccount(
        bool concurrent)
    {
        var identifier = Guid.NewGuid().ToString("N");

        string email = $"{identifier}@example.test";

        string firstOrganization =
            $"Duplicate Test A {identifier}";

        string secondOrganization =
            $"Duplicate Test B {identifier}";

        var first = CreateRequest(
            email,
            firstOrganization);

        var second = CreateRequest(
            email.ToUpperInvariant(),
            secondOrganization);

        var interceptor =
            new ConcurrentRegistrationInterceptor();

        using var factory = _factory.WithWebHostBuilder(
            builder =>
            {
                if (concurrent)
                {
                    builder.ConfigureServices(services =>
                    {
                        services.AddDbContext<StockGuardDbContext>(
                            options =>
                                options.AddInterceptors(interceptor));
                    });
                }
            });

        try
        {
            var services = factory.Services;

            RegistrationResult[] results;

            if (concurrent)
            {
                results = await Task.WhenAll(
                    RegisterAsync(services, first),
                    RegisterAsync(services, second));

                Assert.Equal(2, interceptor.Arrivals);
            }
            else
            {
                results = new[]
                {
                    await RegisterAsync(services, first),
                    await RegisterAsync(services, second)
                };

                Assert.True(results[0].Succeeded);
            }

            var success = Assert.Single(results, x => x.Succeeded);

            var failure = Assert.Single(results, x => !x.Succeeded);

            Assert.NotNull(success.Account);
            Assert.Null(success.ErrorCode);

            Assert.Null(failure.Account);

            Assert.Equal(
                "DuplicateAccount",
                failure.ErrorCode);

            using var scope =
                _factory.Services.CreateScope();

            var context = scope.ServiceProvider
                .GetRequiredService<StockGuardDbContext>();

            VerifyTestDatabase(context);

            string normalizedEmail =
                email.ToUpperInvariant();

            var users = await context.Users
                .AsNoTracking()
                .Where(x =>
                    x.NormalizedEmail == normalizedEmail)
                .ToListAsync();

            var user = Assert.Single(users);

            var organizations = await context.Organizations
                .AsNoTracking()
                .Where(x =>
                    x.Name == firstOrganization ||
                    x.Name == secondOrganization)
                .ToListAsync();

            var organization = Assert.Single(organizations);

            var memberships =
                await context.OrganizationMemberships
                    .AsNoTracking()
                    .Where(x =>
                        x.UserId == user.Id ||
                        x.OrganizationId == organization.Id)
                    .ToListAsync();

            var membership = Assert.Single(memberships);

            Assert.Equal(user.Id, membership.UserId);

            Assert.Equal(
                organization.Id,
                membership.OrganizationId);

            Assert.Equal(
                OrganizationRole.Owner,
                membership.Role);

            Assert.True(membership.IsActive);

            Assert.Equal(
                user.Id,
                success.Account!.UserId);

            Assert.Equal(
                organization.Id,
                success.Account.OrganizationId);
        }
        finally
        {
            using var cleanupScope =
                _factory.Services.CreateScope();

            var cleanup = cleanupScope.ServiceProvider
                .GetRequiredService<StockGuardDbContext>();

            VerifyTestDatabase(cleanup);

            await using var transaction =
                await cleanup.Database.BeginTransactionAsync();

            string normalizedEmail =
                email.ToUpperInvariant();

            var userIds = await cleanup.Users
                .Where(x =>
                    x.NormalizedEmail == normalizedEmail)
                .Select(x => x.Id)
                .ToListAsync();

            var organizationIds = await cleanup.Organizations
                .Where(x =>
                    x.Name == firstOrganization ||
                    x.Name == secondOrganization)
                .Select(x => x.Id)
                .ToListAsync();

            await cleanup.OrganizationMemberships
                .Where(x =>
                    userIds.Contains(x.UserId) ||
                    organizationIds.Contains(x.OrganizationId))
                .ExecuteDeleteAsync();

            await cleanup.Users
                .Where(x => userIds.Contains(x.Id))
                .ExecuteDeleteAsync();

            await cleanup.Organizations
                .Where(x => organizationIds.Contains(x.Id))
                .ExecuteDeleteAsync();

            await transaction.CommitAsync();
        }
    }
}