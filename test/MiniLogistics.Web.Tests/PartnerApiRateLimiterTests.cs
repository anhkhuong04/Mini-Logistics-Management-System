using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using MiniLogistics.Web.Services;
using Xunit;

namespace MiniLogistics.Web.Tests;

public sealed class PartnerApiRateLimiterTests
{
    [Fact]
    public void DistributedLimiter_AppliesQuotaPerApiClientAndEndpointKind()
    {
        var limiter = new DistributedCachePartnerApiRateLimiter(
            new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())),
            Options.Create(new PartnerApiRateLimitOptions
            {
                QuoteLimitPerMinute = 1,
                CreateShipmentLimitPerMinute = 1,
                TrackingLimitPerMinute = 1,
                CancelShipmentLimitPerMinute = 1
            }),
            TestClock.Provider);
        var firstApiClientId = Guid.NewGuid();
        var secondApiClientId = Guid.NewGuid();

        var firstCreateAllowed = limiter.TryAcquire(
            firstApiClientId,
            PartnerApiRateLimitKind.CreateShipment,
            out var firstRetryAfter);
        var secondCreateAllowed = limiter.TryAcquire(
            firstApiClientId,
            PartnerApiRateLimitKind.CreateShipment,
            out var secondRetryAfter);
        var quoteAllowedForSameClient = limiter.TryAcquire(
            firstApiClientId,
            PartnerApiRateLimitKind.Quote,
            out var quoteRetryAfter);
        var createAllowedForOtherClient = limiter.TryAcquire(
            secondApiClientId,
            PartnerApiRateLimitKind.CreateShipment,
            out var otherClientRetryAfter);

        Assert.True(firstCreateAllowed);
        Assert.Equal(TimeSpan.Zero, firstRetryAfter);
        Assert.False(secondCreateAllowed);
        Assert.True(secondRetryAfter >= TimeSpan.FromSeconds(1));
        Assert.True(quoteAllowedForSameClient);
        Assert.Equal(TimeSpan.Zero, quoteRetryAfter);
        Assert.True(createAllowedForOtherClient);
        Assert.Equal(TimeSpan.Zero, otherClientRetryAfter);
    }
}
