using System.Collections.Concurrent;

namespace MiniLogistics.Web.Services;

public sealed class InMemoryPartnerApiRateLimiter : IPartnerApiRateLimiter
{
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    private readonly ConcurrentDictionary<RateLimitKey, WindowCounter> _counters = [];

    public bool TryAcquire(
        Guid apiClientId,
        PartnerApiRateLimitKind kind,
        out TimeSpan retryAfter)
    {
        var now = DateTimeOffset.UtcNow;
        var limit = GetLimit(kind);
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

    private static int GetLimit(PartnerApiRateLimitKind kind)
    {
        return kind switch
        {
            PartnerApiRateLimitKind.Quote => 60,
            PartnerApiRateLimitKind.CreateShipment => 30,
            PartnerApiRateLimitKind.Tracking => 120,
            PartnerApiRateLimitKind.CancelShipment => 30,
            _ => 60
        };
    }

    private sealed record RateLimitKey(Guid ApiClientId, PartnerApiRateLimitKind Kind);

    private sealed record WindowCounter(DateTimeOffset WindowStartedAtUtc, int Count);
}
