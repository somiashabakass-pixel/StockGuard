using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StockGuard.Infrastructure.Data.Entities;
using Xunit;

namespace StockGuard.Tests.Http;

public sealed class IdentityConfigurationTests
    : IClassFixture<StockGuardApiFactory>
{
    private readonly StockGuardApiFactory _factory;

    public IdentityConfigurationTests(
        StockGuardApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void UserManager_CanBeResolved()
    {
        using var scope = _factory.Services.CreateScope();

        var manager = scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();

        Assert.NotNull(manager);
    }

    [Fact]
    public void PasswordPolicy_RequiresMinimumLengthOf15()
    {
        using var scope = _factory.Services.CreateScope();

        var options = scope.ServiceProvider
            .GetRequiredService<IOptions<IdentityOptions>>()
            .Value.Password;

        Assert.Equal(15, options.RequiredLength);
        Assert.False(options.RequireDigit);
        Assert.False(options.RequireUppercase);
        Assert.False(options.RequireLowercase);
        Assert.False(options.RequireNonAlphanumeric);
        Assert.Equal(1, options.RequiredUniqueChars);
    }

    [Fact]
    public async Task ShortPassword_IsRejected()
    {
        using var scope = _factory.Services.CreateScope();

        var manager = scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();

        var user = new ApplicationUser();

        foreach (var validator in manager.PasswordValidators)
        {
            var result = await validator.ValidateAsync(
                manager,
                user,
                "short");

            Assert.False(result.Succeeded);

            Assert.Contains(
                result.Errors,
                error => error.Code == "PasswordTooShort");
        }
    }

    [Fact]
    public async Task ValidPassphrase_IsAccepted()
    {
        using var scope = _factory.Services.CreateScope();

        var manager = scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();

        var user = new ApplicationUser();

        foreach (var validator in manager.PasswordValidators)
        {
            var result = await validator.ValidateAsync(
                manager,
                user,
                "a long secure test passphrase");

            Assert.True(result.Succeeded);
        }
    }

    [Fact]
    public void PasswordHash_DoesNotContainPlaintext()
    {
        using var scope = _factory.Services.CreateScope();

        var hasher = scope.ServiceProvider
            .GetRequiredService<IPasswordHasher<ApplicationUser>>();

        var user = new ApplicationUser();

        const string password = "a secure test passphrase";

        var hash = hasher.HashPassword(user, password);

        Assert.NotEmpty(hash);
        Assert.NotEqual(password, hash);
        Assert.DoesNotContain(password, hash);
    }

    [Fact]
    public void CorrectPassword_IsVerified()
    {
        using var scope = _factory.Services.CreateScope();

        var hasher = scope.ServiceProvider
            .GetRequiredService<IPasswordHasher<ApplicationUser>>();

        var user = new ApplicationUser();

        const string password = "a secure test passphrase";

        var hash = hasher.HashPassword(user, password);

        var result = hasher.VerifyHashedPassword(
            user,
            hash,
            password);

        Assert.Equal(
            PasswordVerificationResult.Success,
            result);
    }

    [Fact]
    public void IncorrectPassword_IsRejected()
    {
        using var scope = _factory.Services.CreateScope();

        var hasher = scope.ServiceProvider
            .GetRequiredService<IPasswordHasher<ApplicationUser>>();

        var user = new ApplicationUser();

        var hash = hasher.HashPassword(
            user,
            "a secure test passphrase");

        var result = hasher.VerifyHashedPassword(
            user,
            hash,
            "an incorrect passphrase");

        Assert.Equal(
            PasswordVerificationResult.Failed,
            result);
    }

    [Fact]
    public void IdenticalPasswords_ProduceDifferentHashes()
    {
        using var scope = _factory.Services.CreateScope();

        var hasher = scope.ServiceProvider
            .GetRequiredService<IPasswordHasher<ApplicationUser>>();

        var user = new ApplicationUser();

        const string password = "a secure test passphrase";

        var firstHash = hasher.HashPassword(user, password);
        var secondHash = hasher.HashPassword(user, password);

        Assert.NotEqual(firstHash, secondHash);
    }

    [Fact]
    public void UniqueEmail_IsRequired()
    {
        using var scope = _factory.Services.CreateScope();

        var options = scope.ServiceProvider
            .GetRequiredService<IOptions<IdentityOptions>>()
            .Value;

        Assert.True(options.User.RequireUniqueEmail);
    }

    [Fact]
    public void AccountLockoutPolicy_IsConfigured()
    {
        using var scope = _factory.Services.CreateScope();

        var options = scope.ServiceProvider
            .GetRequiredService<IOptions<IdentityOptions>>()
            .Value.Lockout;

        Assert.True(options.AllowedForNewUsers);
        Assert.Equal(5, options.MaxFailedAccessAttempts);

        Assert.Equal(
            TimeSpan.FromMinutes(15),
            options.DefaultLockoutTimeSpan);
    }

    [Fact]
    public void EmailConfirmation_IsRequired()
    {
        using var scope = _factory.Services.CreateScope();

        var options = scope.ServiceProvider
            .GetRequiredService<IOptions<IdentityOptions>>()
            .Value;

        Assert.True(options.SignIn.RequireConfirmedEmail);
    }

    [Fact]
    public void PasswordHasher_UsesConfiguredIterations()
    {
        using var scope = _factory.Services.CreateScope();

        var options = scope.ServiceProvider
            .GetRequiredService<IOptions<PasswordHasherOptions>>()
            .Value;

        Assert.Equal(
            PasswordHasherCompatibilityMode.IdentityV3,
            options.CompatibilityMode);

        Assert.Equal(210_000, options.IterationCount);
    }
}