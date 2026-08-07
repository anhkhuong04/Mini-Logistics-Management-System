using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MiniLogistics.Web.Services;

public sealed class RedisPartnerApiRateLimiter : IPartnerApiRateLimiter
{
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    private readonly IRedisRateLimitStore _store;
    private readonly PartnerApiRateLimitOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<RedisPartnerApiRateLimiter> _logger;
    private readonly string _environment;
    private int _consecutiveStoreFailures;
    private long _circuitOpenUntilUnixMilliseconds;

    public RedisPartnerApiRateLimiter(
        IRedisRateLimitStore store,
        IOptions<PartnerApiRateLimitOptions> options,
        TimeProvider timeProvider,
        IHostEnvironment hostEnvironment,
        ILogger<RedisPartnerApiRateLimiter> logger)
    {
        _store = store;
        _options = options.Value;
        _timeProvider = timeProvider;
        _environment = hostEnvironment.EnvironmentName.ToLowerInvariant();
        _logger = logger;
    }

    public async ValueTask<PartnerApiRateLimitDecision> AcquireAsync(
        Guid apiClientId,
        PartnerApiRateLimitKind kind,
        CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow();
        if (now.ToUnixTimeMilliseconds() < Volatile.Read(ref _circuitOpenUntilUnixMilliseconds))
        {
            PartnerApiTelemetry.RecordRateLimitCircuitOpen(kind);
            return StoreUnavailableDecision(kind);
        }

        var windowStart = now.ToUnixTimeSeconds() / (long)Window.TotalSeconds * (long)Window.TotalSeconds;
        var windowEnd = DateTimeOffset.FromUnixTimeSeconds(windowStart).Add(Window);
        var ttl = windowEnd - now;
        var key = $"{_options.KeyPrefix}:{_environment}:partner-api-rate:v1:{apiClientId:N}:{kind}:{windowStart}";

        try
        {
            var increment = await _store
                .IncrementAsync(key, ttl, cancellationToken)
                .WaitAsync(TimeSpan.FromMilliseconds(_options.StoreTimeoutMilliseconds), cancellationToken);
            Interlocked.Exchange(ref _consecutiveStoreFailures, 0);
            Interlocked.Exchange(ref _circuitOpenUntilUnixMilliseconds, 0);
            return increment.Count <= _options.GetLimit(kind)
                ? PartnerApiRateLimitDecision.Allowed
                : new PartnerApiRateLimitDecision(false, EnsureRetryAfter(increment.TimeToLive));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            var failClosed = _options.FailClosed(kind);
            var failures = Interlocked.Increment(ref _consecutiveStoreFailures);
            if (failures >= _options.StoreFailureThreshold)
            {
                Interlocked.Exchange(
                    ref _circuitOpenUntilUnixMilliseconds,
                    now.AddSeconds(_options.CircuitBreakSeconds).ToUnixTimeMilliseconds());
            }
            PartnerApiTelemetry.RecordRateLimitStoreError(kind);
            _logger.LogError(
                exception,
                "Redis rate limit store unavailable for API client {ApiClientId}, action {RateLimitKind}; fail closed: {FailClosed}.",
                apiClientId,
                kind,
                failClosed);
            return StoreUnavailableDecision(kind);
        }
    }

    private PartnerApiRateLimitDecision StoreUnavailableDecision(PartnerApiRateLimitKind kind)
    {
        return _options.FailClosed(kind)
            ? new PartnerApiRateLimitDecision(false, TimeSpan.FromSeconds(1), StoreUnavailable: true)
            : PartnerApiRateLimitDecision.Allowed;
    }

    private static TimeSpan EnsureRetryAfter(TimeSpan value)
    {
        return value < TimeSpan.FromSeconds(1) ? TimeSpan.FromSeconds(1) : value;
    }
}
