using System.ComponentModel.DataAnnotations;

namespace StockGuard.Api.Dtos.Auth;

public sealed class RegisterRequest
{
    [Required]
    [StringLength(100)]
    public string FirstName { get; init; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string LastName { get; init; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string OrganizationName { get; init; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string Email { get; init; } = string.Empty;

    [Required]
    [StringLength(128, MinimumLength = 15)]
    [DataType(DataType.Password)]
    public string Password { get; init; } = string.Empty;

    [Required]
    [StringLength(128)]
    [DataType(DataType.Password)]
    [Compare(nameof(Password))]
    public string ConfirmPassword { get; init; } = string.Empty;
}