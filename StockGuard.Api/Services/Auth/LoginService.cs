using System.ComponentModel.DataAnnotations;
using System.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using StockGuard.Api.Dtos.Auth;
using StockGuard.Infrastructure.Data;
using StockGuard.Infrastructure.Data.Entities;

namespace StockGuard.Api.Services.Auth;

public sealed class LoginService
{
    private readonly StockGuardDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ILoginTimingProtection _timingProtection;

    public LoginService(
        StockGuardDbContext context,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ILoginTimingProtection timingProtection)
    {
        _context = context;
        _userManager = userManager;
        _signInManager = signInManager;
        _timingProtection = timingProtection;
    }

    public async Task<LoginResult> VerifyCredentialsAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        cancellationToken.ThrowIfCancellationRequested();

        var normalizedRequest = new LoginRequest
        {
            Email = request.Email?.Trim() ?? string.Empty,
            Password = request.Password ?? string.Empty,
            RememberMe = request.RememberMe
        };

        bool isValid = Validator.TryValidateObject(
            normalizedRequest,
            new ValidationContext(normalizedRequest),
            new List<ValidationResult>(),
            validateAllProperties: true);

        if (!isValid ||
            normalizedRequest.Email.Any(char.IsControl))
        {
            return LoginResult.Failure();
        }

        var normalizedEmail = _userManager.NormalizeEmail(
            normalizedRequest.Email);

        await using var transaction =
            await _context.Database.BeginTransactionAsync(
                IsolationLevel.ReadCommitted,
                cancellationToken);

        var users = await _context.Users
            .FromSqlInterpolated(
                $"""
                SELECT *
                FROM users
                WHERE "NormalizedEmail" = {normalizedEmail}
                FOR UPDATE
                """)
            .AsTracking()
            .ToListAsync(cancellationToken);

        var user = users.SingleOrDefault();

        if (user is null)
        {
            await transaction.CommitAsync(cancellationToken);

            _timingProtection.PerformDummyVerification(
                normalizedRequest.Password);

            cancellationToken.ThrowIfCancellationRequested();

            return LoginResult.Failure();
        }

        await _context.Entry(user)
            .ReloadAsync(cancellationToken);

        if (!user.IsActive ||
            !await _signInManager.CanSignInAsync(user) ||
            await _userManager.IsLockedOutAsync(user))
        {
            await transaction.CommitAsync(cancellationToken);

            _timingProtection.PerformDummyVerification(
                normalizedRequest.Password);

            cancellationToken.ThrowIfCancellationRequested();

            return LoginResult.Failure();
        }

        var result = await _signInManager.CheckPasswordSignInAsync(
            user,
            normalizedRequest.Password,
            lockoutOnFailure: true);

        using var securityCommitTimeout =
            new CancellationTokenSource(
                TimeSpan.FromSeconds(10));

        await transaction.CommitAsync(
            securityCommitTimeout.Token);

        cancellationToken.ThrowIfCancellationRequested();

        if (!result.Succeeded || user.TwoFactorEnabled)
        {
            return LoginResult.Failure();
        }

        return LoginResult.Success(user.Id);
    }
}
