using System.Globalization;
using System.Text;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace MiniLogistics.Web.Services;

public sealed class DistributedCacheShopUiActionRateLimiter : IShopUiActionRateLimiter
{
    private const int WindowSeconds = 60;

    private readonly IDistributedCache _cache;
    private readonly ShopUiActionRateLimitOptions _options;
    private readonly TimeProvider _timeProvider;

    public DistributedCacheShopUiActionRateLimiter(
        IDistributedCache cache,
        IOptions<ShopUiActionRateLimitOptions> options,
        TimeProvider timeProvider)
    {
        _cache = cache;
        _options = options.Value;
        _timeProvider = timeProvider;
    }

    public bool TryAcquire(
        Guid userId,
        ShopUiActionKind kind,
        out TimeSpan retryAfter)
    {
        var nowUnixSeconds = _timeProvider.GetUtcNow().ToUnixTimeSeconds();
        var windowStartedAtUnixSeconds = nowUnixSeconds / WindowSeconds * WindowSeconds;
        var windowEndsAtUnixSeconds = windowStartedAtUnixSeconds + WindowSeconds;
        var key = $"shop-ui-rate:{userId:N}:{kind}:{windowStartedAtUnixSeconds}";
        var limit = _options.GetLimit(kind);

        var currentValue = _cache.Get(key);
        var currentCount = currentValue is null
            ? 0
            : ParseCount(currentValue);
        var nextCount = currentCount + 1;
        var ttl = TimeSpan.FromSeconds(Math.Max(1, windowEndsAtUnixSeconds - nowUnixSeconds));

        _cache.Set(
            key,
            Encoding.UTF8.GetBytes(nextCount.ToString(CultureInfo.InvariantCulture)),
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = ttl
            });

        if (nextCount <= limit)
        {
            retryAfter = TimeSpan.Zero;
            return true;
        }

        retryAfter = ttl < TimeSpan.FromSeconds(1)
            ? TimeSpan.FromSeconds(1)
            : ttl;
        return false;
    }

    private static int ParseCount(byte[] value)
    {
        return int.TryParse(
            Encoding.UTF8.GetString(value),
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out var count)
            ? count
            : 0;
    }
}

