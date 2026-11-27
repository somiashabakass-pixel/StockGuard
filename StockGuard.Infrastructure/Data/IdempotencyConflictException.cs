namespace StockGuard.Infrastructure.Data;

public sealed class IdempotencyConflictException : Exception
{
    public IdempotencyConflictException()
        : base(
            "The idempotency key has already been used " +
            "with a different audit request.")
    {
    }
}
