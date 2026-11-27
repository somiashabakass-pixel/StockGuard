namespace StockGuard.Api.Dtos.Auth;

public sealed class RegisterResponse
{
    public Guid UserId { get; init; }

    public Guid OrganizationId { get; init; }

    public string FirstName { get; init; } = string.Empty;

    public string LastName { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public string OrganizationName { get; init; } = string.Empty;

    public string Role { get; init; } = string.Empty;

    public bool EmailConfirmationRequired { get; init; }

    public DateTime CreatedAtUtc { get; init; }
}