using Microsoft.AspNetCore.Http;

namespace StockGuard.Api.Helpers;

public static class IdempotencyHeader
{
    public const string HeaderName = "Idempotency-Key";
    public const int MaxKeyLength = 128;

    public static bool TryRead(
        IHeaderDictionary headers,
        out string? key,
        out string? error)
    {
        key = null;
        error = null;

        if (!headers.TryGetValue(
            HeaderName,
            out var values))
        {
            return true;
        }

        if (values.Count != 1)
        {
            error = "Exactly one idempotency key is allowed.";
            return false;
        }

        string? value = values[0];

        if (string.IsNullOrWhiteSpace(value))
        {
            error = "The idempotency key cannot be empty.";
            return false;
        }

        if (value.Length > MaxKeyLength)
        {
            error = "The idempotency key cannot exceed 128 characters.";
            return false;
        }

        if (value.Any(c =>
            c < 33 || c > 126 || c == ','))
        {
            error = "The idempotency key contains invalid characters.";
            return false;
        }

        key = value;

        return true;
    }
}
