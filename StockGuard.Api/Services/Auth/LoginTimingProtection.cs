using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using StockGuard.Infrastructure.Data.Entities;

namespace StockGuard.Api.Services.Auth;

public sealed class LoginTimingProtection : ILoginTimingProtection
{
    private readonly ApplicationUser _dummyUser;
    private readonly PasswordHasher<ApplicationUser> _hasher;
    private readonly string _dummyHash;

    public LoginTimingProtection(
        IOptions<PasswordHasherOptions> options)
    {
        _dummyUser = new ApplicationUser();

        _hasher = new PasswordHasher<ApplicationUser>(options);

        var randomPassword = Convert.ToHexString(
            RandomNumberGenerator.GetBytes(32));

        _dummyHash = _hasher.HashPassword(
            _dummyUser,
            randomPassword);
    }

    public void PerformDummyVerification(string password)
    {
        ArgumentNullException.ThrowIfNull(password);

        _ = _hasher.VerifyHashedPassword(
            _dummyUser,
            _dummyHash,
            password);
    }
}
