using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using StockGuard.Api.Dtos.Auth;
using StockGuard.Api.Services.Auth;
using StockGuard.Infrastructure.Data;
using StockGuard.Infrastructure.Data.Entities;
using Xunit;

namespace StockGuard.Tests.Http;

public sealed class RegistrationRollbackTests
    : IClassFixture<StockGuardApiFactory>
{
    private readonly StockGuardApiFactory _factory;

    public RegistrationRollbackTests(
        StockGuardApiFactory factory)
    {
        _factory = factory;
    }

    private sealed class RegistrationFailureInterceptor
        : SaveChangesInterceptor
    {
        private readonly bool _cancel;

        public bool UserSaved { get; private set; }

        public bool FailureTriggered { get; private set; }

        public Guid UserId { get; private set; }

        public Guid OrganizationId { get; private set; }

        public RegistrationFailureInterceptor(bool cancel)
        {
            _cancel = cancel;
        }

        public override ValueTask<int> SavedChangesAsync(
            SaveChangesCompletedEventData eventData,
            int result,
            CancellationToken cancellationToken = default)
        {
            var user = eventData.Context?
                .ChangeTracker
                .Entries<ApplicationUser>()
                .SingleOrDefault();

            if (user is not null && result > 0)
            {
                UserSaved = true;
                UserId = user.Entity.Id;
            }

            return ValueTask.FromResult(result);
        }

        public override ValueTask<InterceptionResult<int>>
            SavingChangesAsync(
                DbContextEventData eventData,
                InterceptionResult<int> result,
                CancellationToken cancellationToken = default)
        {
            var organization = eventData.Context?
                .ChangeTracker
                .Entries<Organization>()
                .SingleOrDefault(
                    x => x.State == EntityState.Added);

            if (organization is null)
            {
                return ValueTask.FromResult(result);
            }

            OrganizationId = organization.Entity.Id;

            if (!UserSaved)
            {
                throw new InvalidOperationException(
                    "User insertion was not observed.");
            }

            FailureTriggered = true;

            if (_cancel)
            {
                throw new OperationCanceledException(
                    "Simulated registration cancellation.");
            }

            throw new InvalidOperationException(
                "Simulated organization save failure.");
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InterruptedRegistration_RollsBackAllRecords(
        bool cancel)
    {
        var identifier = Guid.NewGuid().ToString("N");

        var request = new RegisterRequest
        {
            FirstName = "Rollback",
            LastName = "Test",
            OrganizationName = $"Rollback Test {identifier}",
            Email = $"{identifier}@example.test",
            Password = "secure rollback test passphrase",
            ConfirmPassword = "secure rollback test passphrase"
        };

        var interceptor =
            new RegistrationFailureInterceptor(cancel);

        using var factory = _factory.WithWebHostBuilder(
            builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services.AddDbContext<StockGuardDbContext>(
                        options =>
                            options.AddInterceptors(interceptor));
                });
            });

        try
        {
            using (var scope = factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider
                    .GetRequiredService<StockGuardDbContext>();

                Assert.Equal(
                    "stockguard_test",
                    context.Database.GetDbConnection().Database);

                var service = scope.ServiceProvider
                    .GetRequiredService<RegistrationService>();

                if (cancel)
                {
                    await Assert.ThrowsAnyAsync<
                        OperationCanceledException>(
                            () => service.RegisterAsync(request));
                }
                else
                {
                    var exception =
                        await Assert.ThrowsAsync<
                            InvalidOperationException>(
                                () => service.RegisterAsync(request));

                    Assert.Equal(
                        "Simulated organization save failure.",
                        exception.Message);
                }

                Assert.True(interceptor.UserSaved);
                Assert.True(interceptor.FailureTriggered);

                Assert.NotEqual(
                    Guid.Empty,
                    interceptor.UserId);

                Assert.NotEqual(
                    Guid.Empty,
                    interceptor.OrganizationId);
            }

            using var verificationScope =
                _factory.Services.CreateScope();

            var verification = verificationScope
                .ServiceProvider
                .GetRequiredService<StockGuardDbContext>();

            Assert.Equal(
                "stockguard_test",
                verification.Database.GetDbConnection().Database);

            Assert.False(
                await verification.Users.AnyAsync(
                    x => x.Id == interceptor.UserId ||
                         x.NormalizedEmail ==
                            request.Email.ToUpperInvariant()));

            Assert.False(
                await verification.Organizations.AnyAsync(
                    x => x.Id == interceptor.OrganizationId ||
                         x.Name == request.OrganizationName));

            Assert.False(
                await verification.OrganizationMemberships.AnyAsync(
                    x => x.UserId == interceptor.UserId ||
                         x.OrganizationId ==
                            interceptor.OrganizationId));
        }
        finally
        {
            using var cleanupScope =
                _factory.Services.CreateScope();

            var cleanup = cleanupScope.ServiceProvider
                .GetRequiredService<StockGuardDbContext>();

            if (cleanup.Database.GetDbConnection().Database
                != "stockguard_test")
            {
                throw new InvalidOperationException(
                    "Cleanup requires stockguard_test.");
            }

            await using var transaction =
                await cleanup.Database.BeginTransactionAsync();

            var userIds = await cleanup.Users
                .Where(x =>
                    x.NormalizedEmail ==
                        request.Email.ToUpperInvariant())
                .Select(x => x.Id)
                .ToListAsync();

            var organizationIds = await cleanup.Organizations
                .Where(x => x.Name == request.OrganizationName)
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