using System.Text.Json;
using MiniLogistics.Web.Endpoints.Browser;
using MiniLogistics.Domain.Shipments;
using Xunit;

namespace MiniLogistics.Web.Tests.Browser;

public sealed class BrowserApiContractTests
{
    private sealed record Payload(decimal Amount, decimal? Optional, ShipmentStatus Status, DateTimeOffset At);

    [Fact]
    public void BrowserJson_PreservesDecimalAndNormalizesUtcWithoutChangingPartnerDefaults()
    {
        var value = new Payload(9007199254740993.01m, null, ShipmentStatus.Delivered,
            DateTimeOffset.Parse("2026-10-01T07:00:00+07:00"));
        var json = JsonSerializer.Serialize(value, BrowserApiJsonOptions.Value);
        Assert.Contains("\"amount\":\"9007199254740993.01\"", json);
        Assert.Contains("\"status\":\"Delivered\"", json);
        Assert.Contains("2026-10-01T00:00:00", json);
        Assert.Equal(value.Amount, JsonSerializer.Deserialize<Payload>(json, BrowserApiJsonOptions.Value)!.Amount);
        Assert.Contains("\"Amount\":9007199254740993.01", JsonSerializer.Serialize(value));
    }

    [Theory]
    [InlineData("1e2")]
    [InlineData("1,23")]
    [InlineData("NaN")]
    [InlineData("79228162514264337593543950336")]
    public void BrowserDecimal_RejectsInvalidAndOverflowValues(string amount) =>
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<decimal>($"\"{amount}\"", BrowserApiJsonOptions.Value));

    [Theory]
    [InlineData("999")]
    [InlineData("\"999\"")]
    [InlineData("\"Unknown\"")]
    public void BrowserEnum_RejectsNumbersAndUndefinedNames(string value) =>
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ShipmentStatus>(value, BrowserApiJsonOptions.Value));

    [Theory]
    [InlineData("Application.ValidationFailed", 400)]
    [InlineData("Application.Forbidden", 403)]
    [InlineData("Application.NotFound", 404)]
    [InlineData("Application.Conflict", 409)]
    [InlineData("Application.ConcurrencyConflict", 409)]
    [InlineData("Application.CapacityReached", 409)]
    [InlineData("Browser.RateLimitExceeded", 429)]
    [InlineData("Application.Unavailable", 503)]
    public void ErrorMapping_IsStable(string code, int status) => Assert.Equal(status, BrowserApiResultMapper.StatusFor(code));
}
