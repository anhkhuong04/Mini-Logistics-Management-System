using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;
using MiniLogistics.Domain.Fees;
using MiniLogistics.Domain.Shipments;
using MiniLogistics.Domain.ValueObjects;
using MiniLogistics.Infrastructure.Persistence;
using MiniLogistics.Web.Services;
using Xunit;

namespace MiniLogistics.Web.Tests.Browser;

public sealed class BrowserPublicTrackingTests
{
    [Fact]
    public async Task AnonymousSummaryIgnoresVerificationClaimsAndNeverSerializesPrivateFields()
    {
        await using var factory = Factory();
        await Seed(factory);
        var response = await Get(factory, "ML-PUBLIC-001?phoneLast4=4567&accessLevel=Verified");
        Assert.Equal(200, response.Response.StatusCode);
        Assert.Equal("no-store", response.Response.Headers.CacheControl);
        var body = await Body(response);
        using var json = JsonDocument.Parse(body);
        var value = json.RootElement;
        Assert.Equal("Summary", value.GetProperty("accessLevel").GetString());
        Assert.Equal("P***", value.GetProperty("senderName").GetString());
        Assert.Equal("P***", value.GetProperty("receiverName").GetString());
        Assert.Equal(BrowserSessionReader.ServerTimeZoneId, value.GetProperty("serverTimeZoneId").GetString());
        Assert.NotEmpty(value.GetProperty("timeline").EnumerateArray());
        foreach (var field in new[] { "senderPhone", "receiverPhone", "pickupAddress", "deliveryAddress", "note", "shopId" })
            Assert.False(value.TryGetProperty(field, out _));
        foreach (var secret in new[] { "0901234567", "Private Sender", "Private Receiver", "Pickup private", "Delivery private", "Private note" })
            Assert.DoesNotContain(secret, body);
    }

    [Theory]
    [InlineData("ML-PUBLIC-DRAFT", 404)]
    [InlineData("ML-PUBLIC-MISSING", 404)]
    [InlineData("%20", 400)]
    public async Task DraftMissingAndMalformedNeverDiscloseShipment(string code, int status)
    {
        await using var factory = Factory();
        await Seed(factory);
        var response = await Get(factory, code);
        Assert.Equal(status, response.Response.StatusCode);
        var body = await Body(response);
        Assert.Contains("application/problem+json", response.Response.ContentType);
        Assert.DoesNotContain("Private", body);
        Assert.DoesNotContain("ML-PUBLIC-DRAFT", body);
    }

    [Theory]
    [InlineData("10.0.0.10", true)]
    [InlineData("198.51.100.15", false)]
    public async Task RateLimitUsesTrustedTransportAddressAndCannotBeResetBySpoofing(string remote, bool trusted)
    {
        await using var factory = Factory(limit: 1);
        Assert.Equal(404, (await Get(factory, "ML-PUBLIC-MISSING", remote, "203.0.113.20")).Response.StatusCode);
        var repeated = await Get(factory, "ML-PUBLIC-MISSING", remote, "203.0.113.20");
        Assert.Equal(429, repeated.Response.StatusCode);
        Assert.InRange(int.Parse(repeated.Response.Headers.RetryAfter.ToString()), 1, 60);
        Assert.Contains("Browser.RateLimitExceeded", await Body(repeated));
        var changedClaim = await Get(factory, "ML-PUBLIC-MISSING", remote, "203.0.113.21");
        Assert.Equal(trusted ? 404 : 429, changedClaim.Response.StatusCode);
        Assert.Equal(404, (await Get(factory, "ML-PUBLIC-MISSING", "198.51.100.16")).Response.StatusCode);
    }

    [Fact]
    public async Task MissingTransportAddressFailsSafelyWithoutSharedFallbackKey()
    {
        await using var factory = Factory();
        var response = await Get(factory, "ML-PUBLIC-MISSING", remote: null);
        Assert.Equal(503, response.Response.StatusCode);
        Assert.Contains("Application.Unavailable", await Body(response));
    }

    private static BrowserIdentityFactory Factory(int limit = 20) => new(configureServices: services =>
    {
        services.Configure<PublicTrackingRateLimitOptions>(options => options.LimitPerMinute = limit);
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.KnownProxies.Clear();
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Add(IPAddress.Parse("10.0.0.10"));
        });
    });

    private static async Task<HttpContext> Get(BrowserIdentityFactory factory, string code,
        string? remote = "198.51.100.15", string? forwarded = null)
    {
        _ = factory.Services;
        return await factory.Server.SendAsync(context =>
        {
            context.Request.Scheme = "https";
            context.Request.Host = new HostString("localhost");
            context.Request.Method = "GET";
            var uri = new Uri("https://localhost/api/v1/public/tracking/" + code);
            context.Request.Path = Uri.UnescapeDataString(uri.AbsolutePath);
            context.Request.QueryString = new QueryString(uri.Query);
            context.Connection.RemoteIpAddress = remote is null ? null : IPAddress.Parse(remote);
            if (forwarded is not null)
            {
                context.Request.Headers["X-Forwarded-For"] = forwarded;
                context.Request.Headers["X-Forwarded-Proto"] = "https";
            }
        });
    }

    private static async Task<string> Body(HttpContext response) => await new StreamReader(response.Response.Body).ReadToEndAsync();

    private static async Task Seed(BrowserIdentityFactory factory)
    {
        var owner = await factory.AddUser("public-owner@example.test", "Shop");
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MiniLogisticsDbContext>();
        db.Shipments.Add(BrowserShopShipmentReadTests.NewShipment(Guid.NewGuid(), owner, "ML-PUBLIC-001"));
        db.Shipments.Add(Shipment.CreateDraft(Guid.NewGuid(), "Private Sender", new PhoneNumber("0901234567"),
            "Private Receiver", new PhoneNumber("0901234567"), new Address("Pickup private", "Ward", "Province", "Vietnam"),
            new Address("Delivery private", "Ward", "Province", "Vietnam"), new Weight(1), new ParcelDimensions(10, 10, 10),
            new Weight(1), Money.Zero, Money.Zero, new ShippingFeeBreakdown(Money.Zero, Money.Zero, Money.Zero, Money.Zero),
            RouteType.IntraProvince, owner, DateTimeOffset.UtcNow, trackingCode: new TrackingCode("ML-PUBLIC-DRAFT")));
        await db.SaveChangesAsync();
    }
}
