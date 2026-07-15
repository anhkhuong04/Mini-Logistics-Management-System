using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace MiniLogistics.Web.Services;

public sealed class DistributedCachePublicTrackingRateLimiter : IPublicTrackingRateLimiter
{
    private const int WindowSeconds = 60;

    private readonly IDistributedCache _cache;
    private readonly PublicTrackingRateLimitOptions _options;

    public DistributedCachePublicTrackingRateLimiter(
        IDistributedCache cache,
        IOptions<PublicTrackingRateLimitOptions> options)
    {
        _cache = cache;
        _options = options.Value;
    }

    public bool TryAcquire(
        string clientKey,
        string trackingCode,
        out TimeSpan retryAfter)
    {
        var nowUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var windowStartedAtUnixSeconds = nowUnixSeconds / WindowSeconds * WindowSeconds;
        var windowEndsAtUnixSeconds = windowStartedAtUnixSeconds + WindowSeconds;
        var key = $"public-tracking-rate:{HashKey(clientKey, trackingCode)}:{windowStartedAtUnixSeconds}";

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

        if (nextCount <= _options.LimitPerMinute)
        {
            retryAfter = TimeSpan.Zero;
            return true;
        }

        retryAfter = ttl < TimeSpan.FromSeconds(1)
            ? TimeSpan.FromSeconds(1)
            : ttl;
        return false;
    }

    private static string HashKey(string clientKey, string trackingCode)
    {
        var trackingPrefix = NormalizeTrackingPrefix(trackingCode);
        var rawKey = $"{clientKey.Trim()}|{trackingPrefix}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(rawKey));
        return Convert.ToHexString(hash[..16]).ToLowerInvariant();
    }

    private static string NormalizeTrackingPrefix(string trackingCode)
    {
        var normalized = trackingCode.Trim().ToUpperInvariant();
        return normalized.Length <= 6
            ? normalized
            : normalized[..6];
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
