using StackExchange.Redis;

namespace MiniLogistics.Web.Services;

public sealed class RedisRateLimitStore : IRedisRateLimitStore
{
    private const string IncrementScript = """
        local count = redis.call('INCR', KEYS[1])
        if count == 1 then
            redis.call('PEXPIRE', KEYS[1], ARGV[1])
        end
        local ttl = redis.call('PTTL', KEYS[1])
        return { count, ttl }
        """;

    private readonly IConnectionMultiplexer _connectionMultiplexer;

    public RedisRateLimitStore(IConnectionMultiplexer connectionMultiplexer)
    {
        _connectionMultiplexer = connectionMultiplexer;
    }

    public async Task<RedisRateLimitIncrementResult> IncrementAsync(
        string key,
        TimeSpan timeToLive,
        CancellationToken cancellationToken = default)
    {
        var database = _connectionMultiplexer.GetDatabase();
        var ttlMilliseconds = Math.Max(1, (long)Math.Ceiling(timeToLive.TotalMilliseconds));
        var result = (RedisResult[]?)await database.ScriptEvaluateAsync(
            IncrementScript,
            [(RedisKey)key],
            [(RedisValue)ttlMilliseconds])
            ?? throw new RedisException("Redis rate limit script returned no result.");
        var count = (long)result[0];
        var remainingMilliseconds = Math.Max(1, (long)result[1]);
        return new RedisRateLimitIncrementResult(
            count,
            TimeSpan.FromMilliseconds(remainingMilliseconds));
    }
}
