using StockGuard.Api.Dtos.Auth;

namespace StockGuard.Api.Services.Auth;

public sealed record RegistrationResult(
    bool Succeeded,
    RegisterResponse? Account,
    string? ErrorCode)
{
    public static RegistrationResult Success(
        RegisterResponse account)
    {
        return new RegistrationResult(
            true,
            account,
            null);
    }

    public static RegistrationResult Failure(
        string errorCode)
    {
        return new RegistrationResult(
            false,
            null,
            errorCode);
    }
}