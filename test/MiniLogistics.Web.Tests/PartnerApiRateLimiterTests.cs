using System.Collections.Concurrent;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MiniLogistics.Web.Services;
using Xunit;

namespace MiniLogistics.Web.Tests;

public sealed class PartnerApiRateLimiterTests
{
    [Fact]
    public async Task InMemoryLimiter_AppliesQuotaPerApiClientAndEndpointKind()
    {
        var limiter = new InMemoryPartnerApiRateLimiter(
            CreateOptions(limit: 1),
            TestClock.Provider);
        var firstApiClientId = Guid.NewGuid();
        var secondApiClientId = Guid.NewGuid();

        var firstCreate = await limiter.AcquireAsync(firstApiClientId, PartnerApiRateLimitKind.CreateShipment);
        var secondCreate = await limiter.AcquireAsync(firstApiClientId, PartnerApiRateLimitKind.CreateShipment);
        var quoteForSameClient = await limiter.AcquireAsync(firstApiClientId, PartnerApiRateLimitKind.Quote);
        var createForOtherClient = await limiter.AcquireAsync(secondApiClientId, PartnerApiRateLimitKind.CreateShipment);

        Assert.True(firstCreate.IsAllowed);
        Assert.False(secondCreate.IsAllowed);
        Assert.True(secondCreate.RetryAfter >= TimeSpan.FromSeconds(1));
        Assert.True(quoteForSameClient.IsAllowed);
        Assert.True(createForOtherClient.IsAllowed);
    }

    [Fact]
    public async Task RedisLimiter_TwoReplicasDoNotExceedSharedLimitDuringRace()
    {
        const int limit = 20;
        var store = new FakeAtomicRedisRateLimitStore();
        var options = CreateOptions(limit);
        var firstReplica = CreateRedisLimiter(store, options);
        var secondReplica = CreateRedisLimiter(store, options);
        var apiClientId = Guid.NewGuid();

        var requests = Enumerable.Range(0, 100)
            .Select(index => (index % 2 == 0 ? firstReplica : secondReplica)
                .AcquireAsync(apiClientId, PartnerApiRateLimitKind.Tracking)
                .AsTask());
        var decisions = await Task.WhenAll(requests);

        Assert.Equal(limit, decisions.Count(decision => decision.IsAllowed));
        Assert.All(
            decisions.Where(decision => !decision.IsAllowed),
            decision => Assert.True(decision.RetryAfter >= TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public async Task RedisLimiter_StoreFailureFailsClosedForCreateAndOpenForTracking()
    {
        var limiter = CreateRedisLimiter(
            new ThrowingRedisRateLimitStore(),
            CreateOptions(limit: 1));

        var create = await limiter.AcquireAsync(Guid.NewGuid(), PartnerApiRateLimitKind.CreateShipment);
        var tracking = await limiter.AcquireAsync(Guid.NewGuid(), PartnerApiRateLimitKind.Tracking);

        Assert.False(create.IsAllowed);
        Assert.True(create.StoreUnavailable);
        Assert.True(tracking.IsAllowed);
    }

    [Fact]
    public async Task RedisLimiter_AfterFailureThresholdShortCircuitsStoreCalls()
    {
        var store = new CountingThrowingRedisRateLimitStore();
        var options = CreateOptions(limit: 1);
        options.Value.StoreFailureThreshold = 1;
        options.Value.CircuitBreakSeconds = 10;
        var limiter = CreateRedisLimiter(store, options);

        var create = await limiter.AcquireAsync(Guid.NewGuid(), PartnerApiRateLimitKind.CreateShipment);
        var tracking = await limiter.AcquireAsync(Guid.NewGuid(), PartnerApiRateLimitKind.Tracking);

        Assert.False(create.IsAllowed);
        Assert.True(tracking.IsAllowed);
        Assert.Equal(1, store.CallCount);
    }

    private static RedisPartnerApiRateLimiter CreateRedisLimiter(
        IRedisRateLimitStore store,
        IOptions<PartnerApiRateLimitOptions> options)
    {
        return new RedisPartnerApiRateLimiter(
            store,
            options,
            TestClock.Provider,
            new FakeHostEnvironment(),
            NullLogger<RedisPartnerApiRateLimiter>.Instance);
    }

    private static IOptions<PartnerApiRateLimitOptions> CreateOptions(int limit)
    {
        return Options.Create(new PartnerApiRateLimitOptions
        {
            QuoteLimitPerMinute = limit,
            CreateShipmentLimitPerMinute = limit,
            TrackingLimitPerMinute = limit,
            CancelShipmentLimitPerMinute = limit,
            StoreTimeoutMilliseconds = 1000
        });
    }

    private sealed class FakeAtomicRedisRateLimitStore : IRedisRateLimitStore
    {
        private readonly ConcurrentDictionary<string, long> _counts = [];

        public Task<RedisRateLimitIncrementResult> IncrementAsync(
            string key,
            TimeSpan timeToLive,
            CancellationToken cancellationToken = default)
        {
            var count = _counts.AddOrUpdate(key, 1, (_, current) => current + 1);
            return Task.FromResult(new RedisRateLimitIncrementResult(count, timeToLive));
        }
    }

    private sealed class ThrowingRedisRateLimitStore : IRedisRateLimitStore
    {
        public Task<RedisRateLimitIncrementResult> IncrementAsync(
            string key,
            TimeSpan timeToLive,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Redis unavailable.");
        }
    }

    private sealed class CountingThrowingRedisRateLimitStore : IRedisRateLimitStore
    {
        public int CallCount { get; private set; }

        public Task<RedisRateLimitIncrementResult> IncrementAsync(
            string key,
            TimeSpan timeToLive,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            throw new InvalidOperationException("Redis unavailable.");
        }
    }

    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Test";

        public string ApplicationName { get; set; } = "MiniLogistics.Web.Tests";

        public string ContentRootPath { get; set; } = string.Empty;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
