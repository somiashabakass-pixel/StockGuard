namespace StockGuard.Api.Services.Auth;

public sealed class LoginResult
{
    public bool Succeeded { get; private init; }

    public Guid? UserId { get; private init; }

    public static LoginResult Success(Guid userId)
    {
        return new LoginResult
        {
            Succeeded = true,
            UserId = userId
        };
    }

    public static LoginResult Failure()
    {
        return new LoginResult
        {
            Succeeded = false
        };
    }
}
