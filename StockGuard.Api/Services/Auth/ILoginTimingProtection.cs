namespace StockGuard.Api.Services.Auth;

public interface ILoginTimingProtection
{
    void PerformDummyVerification(string password);
}
