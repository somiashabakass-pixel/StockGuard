using Microsoft.AspNetCore.Http;
using StockGuard.Api.Helpers;

namespace StockGuard.Tests;

public class IdempotencyHeaderTests
{
    [Fact]
    public void MissingHeader_ShouldBeAccepted()
    {
        var headers = new HeaderDictionary();

        bool valid = IdempotencyHeader.TryRead(
            headers, out var key, out var error);

        Assert.True(valid);
        Assert.Null(key);
        Assert.Null(error);
    }

    [Theory]
    [InlineData("audit-001")]
    [InlineData("ABC-123_xyz")]
    [InlineData("123456789")]
    public void ValidHeader_ShouldBeAccepted(string value)
    {
        var headers = new HeaderDictionary();

        headers[IdempotencyHeader.HeaderName] = value;

        bool valid = IdempotencyHeader.TryRead(
            headers, out var key, out var error);

        Assert.True(valid);
        Assert.Equal(value, key);
        Assert.Null(error);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("audit 001")]
    [InlineData("audit,001")]
    [InlineData("audit\t001")]
    [InlineData("audit\n001")]
    [InlineData("audit\r001")]
    [InlineData("audit\u007F001")]
    [InlineData("audit\u00E9001")]
    public void InvalidHeader_ShouldBeRejected(string value)
    {
        var headers = new HeaderDictionary();

        headers[IdempotencyHeader.HeaderName] = value;

        bool valid = IdempotencyHeader.TryRead(
            headers, out var key, out var error);

        Assert.False(valid);
        Assert.Null(key);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void MultipleHeaders_ShouldBeRejected()
    {
        var headers = new HeaderDictionary();

        headers[IdempotencyHeader.HeaderName] =
            new[] { "audit-001", "audit-002" };

        bool valid = IdempotencyHeader.TryRead(
            headers, out var key, out var error);

        Assert.False(valid);
        Assert.Null(key);
        Assert.NotNull(error);
    }

    [Theory]
    [InlineData(127, true)]
    [InlineData(128, true)]
    [InlineData(129, false)]
    [InlineData(256, false)]
    public void HeaderLength_ShouldBeValidated(
        int length,
        bool expected)
    {
        var headers = new HeaderDictionary();

        string value = new('A', length);

        headers[IdempotencyHeader.HeaderName] = value;

        bool valid = IdempotencyHeader.TryRead(
            headers, out var key, out var error);

        Assert.Equal(expected, valid);

        if (expected)
        {
            Assert.Equal(value, key);
            Assert.Null(error);
        }
        else
        {
            Assert.Null(key);
            Assert.NotNull(error);
        }
    }
}
