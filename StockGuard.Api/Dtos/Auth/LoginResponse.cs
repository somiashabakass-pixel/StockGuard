namespace StockGuard.Api.Dtos.Auth;

public sealed class LoginResponse
{
    public Guid UserId { get; init; }

    public string FirstName { get; init; } = string.Empty;

    public string LastName { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public DateTimeOffset SessionExpiresAtUtc { get; init; }
}
