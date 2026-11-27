using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using StockGuard.Api.Dtos.Auth;
using StockGuard.Infrastructure.Data;
using StockGuard.Infrastructure.Data.Entities;

namespace StockGuard.Api.Services.Auth;

public sealed class RegistrationService
{
    private readonly StockGuardDbContext _context;

    private readonly UserManager<ApplicationUser> _userManager;

    public RegistrationService(
        StockGuardDbContext context,
        UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<RegistrationResult> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        cancellationToken.ThrowIfCancellationRequested();

        var normalizedRequest = new RegisterRequest
        {
            FirstName = request.FirstName?.Trim() ?? string.Empty,
            LastName = request.LastName?.Trim() ?? string.Empty,
            OrganizationName =
                request.OrganizationName?.Trim() ?? string.Empty,
            Email = request.Email?.Trim() ?? string.Empty,
            Password = request.Password ?? string.Empty,
            ConfirmPassword =
                request.ConfirmPassword ?? string.Empty
        };

        var validationResults =
            new List<ValidationResult>();

        bool isValid = Validator.TryValidateObject(
            normalizedRequest,
            new ValidationContext(normalizedRequest),
            validationResults,
            validateAllProperties: true);

        if (!isValid)
        {
            return RegistrationResult.Failure(
                "InvalidRegistration");
        }

        if (normalizedRequest.FirstName.Any(char.IsControl) ||
            normalizedRequest.LastName.Any(char.IsControl) ||
            normalizedRequest.OrganizationName.Any(char.IsControl) ||
            normalizedRequest.Email.Any(char.IsControl))
        {
            return RegistrationResult.Failure(
                "InvalidRegistration");
        }

        var existingUser =
            await _userManager.FindByEmailAsync(
                normalizedRequest.Email);

        cancellationToken.ThrowIfCancellationRequested();

        if (existingUser is not null)
        {
            return RegistrationResult.Failure(
                "DuplicateAccount");
        }

        var createdAtUtc = DateTime.UtcNow;

        await using var transaction =
            await _context.Database.BeginTransactionAsync(
                cancellationToken);

        try
        {
            var user = new ApplicationUser
            {
                FirstName = normalizedRequest.FirstName,
                LastName = normalizedRequest.LastName,
                Email = normalizedRequest.Email,
                UserName = normalizedRequest.Email,
                EmailConfirmed = false,
                CreatedAtUtc = createdAtUtc,
                IsActive = true
            };

            var identityResult =
                await _userManager.CreateAsync(
                    user,
                    normalizedRequest.Password);

            cancellationToken.ThrowIfCancellationRequested();

            if (!identityResult.Succeeded)
            {
                await transaction.RollbackAsync(
                    CancellationToken.None);

                _context.ChangeTracker.Clear();

                bool duplicate = identityResult.Errors.Any(
                    error => error.Code == "DuplicateEmail" ||
                             error.Code == "DuplicateUserName");

                return RegistrationResult.Failure(
                    duplicate
                        ? "DuplicateAccount"
                        : "InvalidRegistration");
            }

            var organization = new Organization
            {
                Name = normalizedRequest.OrganizationName,
                CreatedAtUtc = createdAtUtc,
                IsActive = true
            };

            var membership = new OrganizationMembership
            {
                UserId = user.Id,
                OrganizationId = organization.Id,
                Role = OrganizationRole.Owner,
                CreatedAtUtc = createdAtUtc,
                IsActive = true
            };

            _context.Organizations.Add(organization);

            _context.OrganizationMemberships.Add(membership);

            await _context.SaveChangesAsync(
                cancellationToken);

            var response = new RegisterResponse
            {
                UserId = user.Id,
                OrganizationId = organization.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = normalizedRequest.Email,
                OrganizationName = organization.Name,
                Role = membership.Role.ToString(),
                EmailConfirmationRequired = true,
                CreatedAtUtc = createdAtUtc
            };

            await transaction.CommitAsync(
                cancellationToken);

            return RegistrationResult.Success(response);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException postgres &&
                  postgres.SqlState == PostgresErrorCodes.UniqueViolation &&
                  (postgres.ConstraintName == "UX_users_normalized_email" ||
                   postgres.ConstraintName == "UserNameIndex"))
        {
            await transaction.RollbackAsync(
                CancellationToken.None);

            _context.ChangeTracker.Clear();

            return RegistrationResult.Failure(
                "DuplicateAccount");
        }
        catch
        {
            await transaction.RollbackAsync(
                CancellationToken.None);

            _context.ChangeTracker.Clear();

            throw;
        }
    }
}