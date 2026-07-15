using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using MiniLogistics.Web.Services;
using Xunit;

namespace MiniLogistics.Web.Tests;

public sealed class PublicTrackingRateLimiterTests
{
    [Fact]
    public void DistributedLimiter_AppliesQuotaPerClientAndTrackingPrefix()
    {
        var limiter = new DistributedCachePublicTrackingRateLimiter(
            new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())),
            Options.Create(new PublicTrackingRateLimitOptions
            {
                LimitPerMinute = 1
            }));

        var firstAllowed = limiter.TryAcquire(
            "203.0.113.10",
            "ML202607150001",
            out var firstRetryAfter);
        var secondAllowed = limiter.TryAcquire(
            "203.0.113.10",
            "ML202607150002",
            out var secondRetryAfter);
        var otherPrefixAllowed = limiter.TryAcquire(
            "203.0.113.10",
            "AB202607150001",
            out var otherPrefixRetryAfter);
        var otherClientAllowed = limiter.TryAcquire(
            "203.0.113.11",
            "ML202607150001",
            out var otherClientRetryAfter);

        Assert.True(firstAllowed);
        Assert.Equal(TimeSpan.Zero, firstRetryAfter);
        Assert.False(secondAllowed);
        Assert.True(secondRetryAfter >= TimeSpan.FromSeconds(1));
        Assert.True(otherPrefixAllowed);
        Assert.Equal(TimeSpan.Zero, otherPrefixRetryAfter);
        Assert.True(otherClientAllowed);
        Assert.Equal(TimeSpan.Zero, otherClientRetryAfter);
    }
}
