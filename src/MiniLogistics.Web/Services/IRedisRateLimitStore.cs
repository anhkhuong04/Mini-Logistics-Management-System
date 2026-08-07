namespace MiniLogistics.Web.Services;

public interface IRedisRateLimitStore
{
    Task<RedisRateLimitIncrementResult> IncrementAsync(
        string key,
        TimeSpan timeToLive,
        CancellationToken cancellationToken = default);
}

public sealed record RedisRateLimitIncrementResult(long Count, TimeSpan TimeToLive);
