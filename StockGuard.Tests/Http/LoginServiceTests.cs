using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StockGuard.Api.Dtos.Auth;
using StockGuard.Api.Services.Auth;
using StockGuard.Infrastructure.Data;
using StockGuard.Infrastructure.Data.Entities;
using Xunit;

namespace StockGuard.Tests.Http;

public sealed class LoginServiceTests
    : IClassFixture<StockGuardApiFactory>
{
    private const string TestPassword =
        "secure login integration test passphrase";

    private readonly StockGuardApiFactory _factory;

    public LoginServiceTests(StockGuardApiFactory factory)
    {
        _factory = factory;
    }

    private static void VerifyDatabase(IServiceProvider services)
    {
        var context =
            services.GetRequiredService<StockGuardDbContext>();

        if (context.Database.GetDbConnection().Database
            != "stockguard_test")
        {
            throw new InvalidOperationException(
                "Login tests require stockguard_test.");
        }
    }

    private async Task WithUserAsync(
        bool active,
        bool confirmed,
        Func<IServiceProvider, ApplicationUser, Task> test)
    {
        using var scope = _factory.Services.CreateScope();

        VerifyDatabase(scope.ServiceProvider);

        var manager = scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();

        string email =
            $"{Guid.NewGuid():N}@example.test";

        var user = new ApplicationUser
        {
            FirstName = "Login",
            LastName = "Test",
            Email = email,
            UserName = email,
            IsActive = active,
            EmailConfirmed = confirmed
        };

        try
        {
            var created = await manager.CreateAsync(
                user, TestPassword);

            Assert.True(created.Succeeded);

            using var testScope =
                _factory.Services.CreateScope();

            VerifyDatabase(testScope.ServiceProvider);

            await test(testScope.ServiceProvider, user);
        }
        finally
        {
            using var cleanupScope =
                _factory.Services.CreateScope();

            VerifyDatabase(cleanupScope.ServiceProvider);

            var cleanupManager = cleanupScope.ServiceProvider
                .GetRequiredService<UserManager<ApplicationUser>>();

            var existing = await cleanupManager.FindByIdAsync(
                user.Id.ToString());

            if (existing is not null)
            {
                var deleted =
                    await cleanupManager.DeleteAsync(existing);

                Assert.True(deleted.Succeeded);
            }
        }
    }

    [Fact]
    public void LoginService_CanBeResolved()
    {
        using var scope = _factory.Services.CreateScope();

        VerifyDatabase(scope.ServiceProvider);

        Assert.NotNull(scope.ServiceProvider
            .GetRequiredService<LoginService>());
    }

    [Theory]
    [InlineData(true, true, true, true)]
    [InlineData(true, true, false, false)]
    [InlineData(false, true, true, false)]
    [InlineData(true, false, true, false)]
    public async Task VerifyCredentials_EnforcesAccountSecurity(
        bool active,
        bool confirmed,
        bool correctPassword,
        bool expectedSuccess)
    {
        await WithUserAsync(
            active,
            confirmed,
            async (services, user) =>
            {
                var service =
                    services.GetRequiredService<LoginService>();

                var result =
                    await service.VerifyCredentialsAsync(
                        new LoginRequest
                        {
                            Email = user.Email!,
                            Password = correctPassword
                                ? TestPassword
                                : "incorrect test passphrase"
                        });

                Assert.Equal(
                    expectedSuccess, result.Succeeded);

                if (expectedSuccess)
                {
                    Assert.Equal<Guid?>(
                        user.Id, result.UserId);
                }
                else
                {
                    Assert.Null(result.UserId);
                }
            });
    }

    [Fact]
    public async Task UnknownEmail_IsRejected()
    {
        using var scope = _factory.Services.CreateScope();

        VerifyDatabase(scope.ServiceProvider);

        var service = scope.ServiceProvider
            .GetRequiredService<LoginService>();

        var result = await service.VerifyCredentialsAsync(
            new LoginRequest
            {
                Email = $"{Guid.NewGuid():N}@example.test",
                Password = TestPassword
            });

        Assert.False(result.Succeeded);
        Assert.Null(result.UserId);
    }

    [Fact]
    public async Task NormalizedEmail_IsAccepted()
    {
        await WithUserAsync(
            true,
            true,
            async (services, user) =>
            {
                var service =
                    services.GetRequiredService<LoginService>();

                var result =
                    await service.VerifyCredentialsAsync(
                        new LoginRequest
                        {
                            Email =
                                $"  {user.Email!.ToUpperInvariant()}  ",
                            Password = TestPassword
                        });

                Assert.True(result.Succeeded);
                Assert.Equal<Guid?>(user.Id, result.UserId);
            });
    }

    [Fact]
    public async Task InvalidRequest_IsRejected()
    {
        using var scope = _factory.Services.CreateScope();

        VerifyDatabase(scope.ServiceProvider);

        var service = scope.ServiceProvider
            .GetRequiredService<LoginService>();

        var result = await service.VerifyCredentialsAsync(
            new LoginRequest
            {
                Email = "invalid-email",
                Password = string.Empty
            });

        Assert.False(result.Succeeded);
        Assert.Null(result.UserId);
    }

    private async Task<LoginResult> AttemptAsync(
        string email,
        string password)
    {
        using var scope = _factory.Services.CreateScope();

        VerifyDatabase(scope.ServiceProvider);

        var service = scope.ServiceProvider
            .GetRequiredService<LoginService>();

        return await service.VerifyCredentialsAsync(
            new LoginRequest
            {
                Email = email,
                Password = password
            });
    }

    private async Task<ApplicationUser> ReloadUserAsync(
        Guid userId)
    {
        using var scope = _factory.Services.CreateScope();

        VerifyDatabase(scope.ServiceProvider);

        var manager = scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();

        var user = await manager.FindByIdAsync(
            userId.ToString());

        Assert.NotNull(user);

        return user!;
    }

    [Fact]
    public async Task FailedPassword_IncrementsCounter()
    {
        await WithUserAsync(true, true, async (_, user) =>
        {
            var result = await AttemptAsync(
                user.Email!, "incorrect test password");

            Assert.False(result.Succeeded);

            var stored = await ReloadUserAsync(user.Id);

            Assert.True(stored.LockoutEnabled);
            Assert.Equal(1, stored.AccessFailedCount);
        });
    }

    [Fact]
    public async Task FiveFailures_TriggerLockout()
    {
        await WithUserAsync(true, true, async (_, user) =>
        {
            for (int attempt = 1; attempt <= 4; attempt++)
            {
                var result = await AttemptAsync(
                    user.Email!, "incorrect test password");

                Assert.False(result.Succeeded);

                var current = await ReloadUserAsync(user.Id);

                Assert.Equal(
                    attempt, current.AccessFailedCount);

                Assert.False(
                    current.LockoutEnd > DateTimeOffset.UtcNow);
            }

            var before = DateTimeOffset.UtcNow;

            var fifth = await AttemptAsync(
                user.Email!, "incorrect test password");

            Assert.False(fifth.Succeeded);

            var after = DateTimeOffset.UtcNow;
            var stored = await ReloadUserAsync(user.Id);

            Assert.NotNull(stored.LockoutEnd);

            Assert.InRange(
                stored.LockoutEnd!.Value,
                before.AddMinutes(15).AddSeconds(-1),
                after.AddMinutes(15).AddSeconds(1));
        });
    }

    [Fact]
    public async Task CorrectPassword_CannotBypassLockout()
    {
        await WithUserAsync(true, true, async (_, user) =>
        {
            for (int attempt = 0; attempt < 5; attempt++)
            {
                var failed = await AttemptAsync(
                    user.Email!, "incorrect test password");

                Assert.False(failed.Succeeded);
            }

            var stored = await ReloadUserAsync(user.Id);

            Assert.True(
                stored.LockoutEnd > DateTimeOffset.UtcNow);

            var result = await AttemptAsync(
                user.Email!, TestPassword);

            Assert.False(result.Succeeded);
            Assert.Null(result.UserId);
        });
    }

    [Fact]
    public async Task SuccessfulLogin_ResetsFailureCounter()
    {
        await WithUserAsync(true, true, async (_, user) =>
        {
            for (int attempt = 0; attempt < 3; attempt++)
            {
                var failed = await AttemptAsync(
                    user.Email!, "incorrect test password");

                Assert.False(failed.Succeeded);
            }

            var before = await ReloadUserAsync(user.Id);

            Assert.Equal(3, before.AccessFailedCount);

            var result = await AttemptAsync(
                user.Email!, TestPassword);

            Assert.True(result.Succeeded);

            var after = await ReloadUserAsync(user.Id);

            Assert.Equal(0, after.AccessFailedCount);
        });
    }

    [Fact]
    public async Task ExpiredLockout_AllowsCorrectPassword()
    {
        await WithUserAsync(true, true, async (_, user) =>
        {
            for (int attempt = 0; attempt < 5; attempt++)
            {
                var failed = await AttemptAsync(
                    user.Email!, "incorrect test password");

                Assert.False(failed.Succeeded);
            }

            using (var scope = _factory.Services.CreateScope())
            {
                VerifyDatabase(scope.ServiceProvider);

                var manager = scope.ServiceProvider
                    .GetRequiredService<
                        UserManager<ApplicationUser>>();

                var stored = await manager.FindByIdAsync(
                    user.Id.ToString());

                Assert.NotNull(stored);

                Assert.True(
                    stored!.LockoutEnd > DateTimeOffset.UtcNow);

                var updated =
                    await manager.SetLockoutEndDateAsync(
                        stored,
                        DateTimeOffset.UtcNow.AddMinutes(-1));

                Assert.True(updated.Succeeded);
            }

            var result = await AttemptAsync(
                user.Email!, TestPassword);

            Assert.True(result.Succeeded);
            Assert.Equal<Guid?>(user.Id, result.UserId);
        });
    }

    [Fact]
    public async Task ConcurrentFailures_EnforceLockout()
    {
        await WithUserAsync(true, true, async (_, user) =>
        {
            var gate = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            int ready = 0;

            var attempts = Enumerable.Range(0, 5)
                .Select(async attempt =>
                {
                    if (Interlocked.Increment(ref ready) == 5)
                    {
                        gate.TrySetResult(true);
                    }

                    await gate.Task.WaitAsync(
                        TimeSpan.FromSeconds(15));

                    return await AttemptAsync(
                        user.Email!,
                        $"incorrect concurrent password {attempt}");
                })
                .ToArray();

            var results = await Task.WhenAll(attempts);

            Assert.All(results, result =>
            {
                Assert.False(result.Succeeded);
                Assert.Null(result.UserId);
            });

            var stored = await ReloadUserAsync(user.Id);

            Assert.True(
                stored.LockoutEnd > DateTimeOffset.UtcNow,
                $"Concurrent failures did not trigger lockout. " +
                $"Stored failure count: {stored.AccessFailedCount}.");

            var correct = await AttemptAsync(
                user.Email!, TestPassword);

            Assert.False(correct.Succeeded);
            Assert.Null(correct.UserId);
        });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MfaEnabledAccount_RejectsPasswordOnlyLogin(
        bool correctPassword)
    {
        await WithUserAsync(
            true,
            true,
            async (services, user) =>
            {
                var manager = services
                    .GetRequiredService<UserManager<ApplicationUser>>();

                var storedUser = await manager.FindByIdAsync(
                    user.Id.ToString());

                Assert.NotNull(storedUser);

                var enabled = await manager.SetTwoFactorEnabledAsync(
                    storedUser,
                    true);

                Assert.True(enabled.Succeeded);

                var persisted = await ReloadUserAsync(user.Id);

                Assert.True(persisted.TwoFactorEnabled);

                var result = await AttemptAsync(
                    user.Email!,
                    correctPassword
                        ? TestPassword
                        : "incorrect MFA test password");

                Assert.False(result.Succeeded);
                Assert.Null(result.UserId);
            });
    }

    private sealed class RecordingTimingProtection
        : ILoginTimingProtection
    {
        private readonly ILoginTimingProtection _inner;

        public int Calls { get; private set; }

        public RecordingTimingProtection(
            ILoginTimingProtection inner)
        {
            _inner = inner;
        }

        public void PerformDummyVerification(string password)
        {
            Calls++;

            _inner.PerformDummyVerification(password);
        }
    }

    private static LoginService CreateObservedLoginService(
        IServiceProvider services,
        out RecordingTimingProtection observer)
    {
        VerifyDatabase(services);

        observer = new RecordingTimingProtection(
            services.GetRequiredService<ILoginTimingProtection>());

        return new LoginService(
            services.GetRequiredService<StockGuardDbContext>(),
            services.GetRequiredService<UserManager<ApplicationUser>>(),
            services.GetRequiredService<SignInManager<ApplicationUser>>(),
            observer);
    }

    [Theory]
    [InlineData("Unknown", 1, false)]
    [InlineData("Disabled", 1, false)]
    [InlineData("Unconfirmed", 1, false)]
    [InlineData("Locked", 1, false)]
    [InlineData("WrongPassword", 0, false)]
    [InlineData("CorrectPassword", 0, true)]
    [InlineData("MfaEnabled", 0, false)]
    [InlineData("InvalidRequest", 0, false)]
    public async Task TimingProtection_RejectionPaths(
        string scenario,
        int expectedCalls,
        bool expectedSuccess)
    {
        if (scenario is "Unknown" or "InvalidRequest")
        {
            using var scope = _factory.Services.CreateScope();

            var service = CreateObservedLoginService(
                scope.ServiceProvider,
                out var observer);

            var result = await service.VerifyCredentialsAsync(
                new LoginRequest
                {
                    Email = scenario == "InvalidRequest"
                        ? "invalid-email"
                        : $"{Guid.NewGuid():N}@example.test",
                    Password = TestPassword
                });

            Assert.False(result.Succeeded);
            Assert.Null(result.UserId);
            Assert.Equal(expectedCalls, observer.Calls);

            return;
        }

        await WithUserAsync(
            scenario != "Disabled",
            scenario != "Unconfirmed",
            async (services, user) =>
            {
                var manager = services
                    .GetRequiredService<UserManager<ApplicationUser>>();

                var stored = await manager.FindByIdAsync(
                    user.Id.ToString());

                Assert.NotNull(stored);

                if (scenario == "Locked")
                {
                    Assert.True(
                        (await manager.SetLockoutEnabledAsync(
                            stored, true)).Succeeded);

                    Assert.True(
                        (await manager.SetLockoutEndDateAsync(
                            stored,
                            DateTimeOffset.UtcNow.AddMinutes(15)))
                        .Succeeded);
                }

                if (scenario == "MfaEnabled")
                {
                    Assert.True(
                        (await manager.SetTwoFactorEnabledAsync(
                            stored, true)).Succeeded);
                }

                using var loginScope =
                    _factory.Services.CreateScope();

                var service = CreateObservedLoginService(
                    loginScope.ServiceProvider,
                    out var observer);

                var result = await service.VerifyCredentialsAsync(
                    new LoginRequest
                    {
                        Email = user.Email!,
                        Password = scenario == "WrongPassword"
                            ? "incorrect timing test password"
                            : TestPassword
                    });

                Assert.Equal(expectedSuccess, result.Succeeded);
                Assert.Equal(expectedCalls, observer.Calls);

                if (expectedSuccess)
                {
                    Assert.Equal<Guid?>(user.Id, result.UserId);
                }
                else
                {
                    Assert.Null(result.UserId);
                }
            });
    }

    private sealed class CancelAfterUserSaveInterceptor
        : Microsoft.EntityFrameworkCore.Diagnostics.SaveChangesInterceptor
    {
        private readonly CancellationTokenSource _source;

        public bool Triggered { get; private set; }

        public CancelAfterUserSaveInterceptor(
            CancellationTokenSource source)
        {
            _source = source;
        }

        public override ValueTask<int> SavedChangesAsync(
            Microsoft.EntityFrameworkCore.Diagnostics.SaveChangesCompletedEventData eventData,
            int result,
            CancellationToken cancellationToken = default)
        {
            if (result > 0 &&
                eventData.Context is not null &&
                eventData.Context.ChangeTracker
                    .Entries<ApplicationUser>().Any())
            {
                Triggered = true;
                _source.Cancel();
            }

            return ValueTask.FromResult(result);
        }
    }

    [Fact]
    public async Task PreCancelledLogin_DoesNotIncrementCounter()
    {
        await WithUserAsync(true, true, async (services, user) =>
        {
            using var cancellation =
                new CancellationTokenSource();

            cancellation.Cancel();

            var service =
                services.GetRequiredService<LoginService>();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => service.VerifyCredentialsAsync(
                    new LoginRequest
                    {
                        Email = user.Email!,
                        Password = "incorrect cancelled password"
                    },
                    cancellation.Token));

            var stored = await ReloadUserAsync(user.Id);

            Assert.Equal(0, stored.AccessFailedCount);
        });
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public async Task CancelledLogin_PreservesSecurityChanges(
        int previousFailures)
    {
        await WithUserAsync(true, true, async (_, user) =>
        {
            for (int i = 0; i < previousFailures; i++)
            {
                var failed = await AttemptAsync(
                    user.Email!,
                    "incorrect preliminary password");

                Assert.False(failed.Succeeded);
            }

            using var cancellation =
                new CancellationTokenSource();

            var interceptor =
                new CancelAfterUserSaveInterceptor(cancellation);

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

            using var scope = factory.Services.CreateScope();

            VerifyDatabase(scope.ServiceProvider);

            var service = scope.ServiceProvider
                .GetRequiredService<LoginService>();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => service.VerifyCredentialsAsync(
                    new LoginRequest
                    {
                        Email = user.Email!,
                        Password = "incorrect cancellation test password"
                    },
                    cancellation.Token));

            Assert.True(interceptor.Triggered);

            var stored = await ReloadUserAsync(user.Id);

            if (previousFailures == 4)
            {
                Assert.True(
                    stored.LockoutEnd > DateTimeOffset.UtcNow);

                var correct = await AttemptAsync(
                    user.Email!, TestPassword);

                Assert.False(correct.Succeeded);
                Assert.Null(correct.UserId);
            }
            else
            {
                Assert.Equal(1, stored.AccessFailedCount);
            }
        });
    }
}
