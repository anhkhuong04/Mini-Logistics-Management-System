using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using MiniLogistics.Web.Services;
using Xunit;

namespace MiniLogistics.Web.Tests;

public sealed class ShopUiActionRateLimiterTests
{
    [Fact]
    public void DistributedLimiter_AppliesQuotaPerUserAndActionKind()
    {
        var limiter = new DistributedCacheShopUiActionRateLimiter(
            new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())),
            Options.Create(new ShopUiActionRateLimitOptions
            {
                ExportShipmentsLimitPerMinute = 1,
                ExportCodReportLimitPerMinute = 1,
                GenerateLabelLimitPerMinute = 1
            }),
            TestClock.Provider);
        var firstUserId = Guid.NewGuid();
        var secondUserId = Guid.NewGuid();

        var firstExportAllowed = limiter.TryAcquire(
            firstUserId,
            ShopUiActionKind.ExportShipments,
            out var firstRetryAfter);
        var secondExportAllowed = limiter.TryAcquire(
            firstUserId,
            ShopUiActionKind.ExportShipments,
            out var secondRetryAfter);
        var codExportAllowedForSameUser = limiter.TryAcquire(
            firstUserId,
            ShopUiActionKind.ExportCodReport,
            out var codRetryAfter);
        var exportAllowedForOtherUser = limiter.TryAcquire(
            secondUserId,
            ShopUiActionKind.ExportShipments,
            out var otherUserRetryAfter);

        Assert.True(firstExportAllowed);
        Assert.Equal(TimeSpan.Zero, firstRetryAfter);
        Assert.False(secondExportAllowed);
        Assert.True(secondRetryAfter >= TimeSpan.FromSeconds(1));
        Assert.True(codExportAllowedForSameUser);
        Assert.Equal(TimeSpan.Zero, codRetryAfter);
        Assert.True(exportAllowedForOtherUser);
        Assert.Equal(TimeSpan.Zero, otherUserRetryAfter);
    }
}

