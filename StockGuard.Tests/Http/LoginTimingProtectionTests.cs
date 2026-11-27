using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StockGuard.Api.Services.Auth;
using Xunit;

namespace StockGuard.Tests.Http;

public sealed class LoginTimingProtectionTests
    : IClassFixture<StockGuardApiFactory>
{
    private readonly StockGuardApiFactory _factory;

    public LoginTimingProtectionTests(
        StockGuardApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void TimingProtection_CanBeResolved()
    {
        using var scope = _factory.Services.CreateScope();

        var service = scope.ServiceProvider
            .GetRequiredService<LoginTimingProtection>();

        Assert.NotNull(service);
    }

    [Fact]
    public void TimingProtection_IsSingleton()
    {
        using var firstScope = _factory.Services.CreateScope();
        using var secondScope = _factory.Services.CreateScope();

        var first = firstScope.ServiceProvider
            .GetRequiredService<LoginTimingProtection>();

        var second = secondScope.ServiceProvider
            .GetRequiredService<LoginTimingProtection>();

        Assert.Same(first, second);
    }

    [Theory]
    [InlineData("incorrect password")]
    [InlineData("")]
    [InlineData("Unicode password \u00E9 \u4E16")]
    public void DummyVerification_Completes(string password)
    {
        using var scope = _factory.Services.CreateScope();

        var service = scope.ServiceProvider
            .GetRequiredService<LoginTimingProtection>();

        var exception = Record.Exception(
            () => service.PerformDummyVerification(password));

        Assert.Null(exception);
    }

    [Fact]
    public void DummyVerification_RejectsNull()
    {
        using var scope = _factory.Services.CreateScope();

        var service = scope.ServiceProvider
            .GetRequiredService<LoginTimingProtection>();

        Assert.Throws<ArgumentNullException>(
            () => service.PerformDummyVerification(null!));
    }

    [Fact]
    public void TimingProtection_UsesConfiguredHashingPolicy()
    {
        using var scope = _factory.Services.CreateScope();

        var options = scope.ServiceProvider
            .GetRequiredService<IOptions<PasswordHasherOptions>>();

        Assert.Equal(
            PasswordHasherCompatibilityMode.IdentityV3,
            options.Value.CompatibilityMode);

        Assert.Equal(210_000, options.Value.IterationCount);
    }

    [Fact]
    public async Task DummyVerification_SupportsConcurrentCalls()
    {
        using var scope = _factory.Services.CreateScope();

        var service = scope.ServiceProvider
            .GetRequiredService<LoginTimingProtection>();

        var tasks = Enumerable.Range(1, 5)
            .Select(index => Task.Run(
                () => service.PerformDummyVerification(
                    $"incorrect password {index}")))
            .ToArray();

        await Task.WhenAll(tasks);
    }
}
