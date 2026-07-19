using System.Collections.Concurrent;
using Microsoft.Extensions.Options;

namespace MiniLogistics.Web.Services;

public sealed class InMemoryPartnerApiRateLimiter : IPartnerApiRateLimiter
{
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    private readonly ConcurrentDictionary<RateLimitKey, WindowCounter> _counters = [];
    private readonly PartnerApiRateLimitOptions _options;
    private readonly TimeProvider _timeProvider;

    public InMemoryPartnerApiRateLimiter(
        IOptions<PartnerApiRateLimitOptions> options,
        TimeProvider timeProvider)
    {
        _options = options.Value;
        _timeProvider = timeProvider;
    }

    public bool TryAcquire(
        Guid apiClientId,
        PartnerApiRateLimitKind kind,
        out TimeSpan retryAfter)
    {
        var now = _timeProvider.GetUtcNow();
        var limit = _options.GetLimit(kind);
        var key = new RateLimitKey(apiClientId, kind);
        var counter = _counters.AddOrUpdate(
            key,
            _ => new WindowCounter(now, 1),
            (_, current) =>
            {
                if (now - current.WindowStartedAtUtc >= Window)
                {
                    return new WindowCounter(now, 1);
                }

                return current with { Count = current.Count + 1 };
            });

        if (counter.Count <= limit)
        {
            retryAfter = TimeSpan.Zero;
            return true;
        }

        retryAfter = Window - (now - counter.WindowStartedAtUtc);
        if (retryAfter < TimeSpan.FromSeconds(1))
        {
            retryAfter = TimeSpan.FromSeconds(1);
        }

        return false;
    }

    private sealed record RateLimitKey(Guid ApiClientId, PartnerApiRateLimitKind Kind);

    private sealed record WindowCounter(DateTimeOffset WindowStartedAtUtc, int Count);
}
