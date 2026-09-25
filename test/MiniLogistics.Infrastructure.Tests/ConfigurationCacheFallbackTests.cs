using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MiniLogistics.Application.Fees;
using MiniLogistics.Application.Routing;
using MiniLogistics.Application.Shippers;
using MiniLogistics.Infrastructure.Persistence;
using MiniLogistics.Infrastructure.Persistence.Repositories;
using StackExchange.Redis;
using Xunit;

namespace MiniLogistics.Infrastructure.Tests;

public sealed class ConfigurationCacheFallbackTests : IClassFixture<LocalDbIntegrationFixture>
{
    private readonly LocalDbIntegrationFixture _fixture;

    public ConfigurationCacheFallbackTests(LocalDbIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task RedisVersionStoreUnavailable_LoadsRouteFeeAndHubConfigurationFromSql()
    {
        await _fixture.ExecuteAsync(async provider =>
        {
            var dbContext = provider.GetRequiredService<MiniLogisticsDbContext>();
            var versionStore = new UnavailableVersionStore();
            var options = Options.Create(new ConfigurationCacheOptions());

            var routeCache = new CachedRouteRegionConfigRepository(
                new RouteRegionConfigRepository(dbContext),
                new MemoryCache(new MemoryCacheOptions()),
                versionStore,
                options,
                Microsoft.Extensions.Logging.Abstractions.NullLogger<CachedRouteRegionConfigRepository>.Instance);
            var feeCache = new FeeRuleCache(
                new FeeRuleRepository(dbContext),
                new MemoryCache(new MemoryCacheOptions()),
                versionStore,
                options,
                Microsoft.Extensions.Logging.Abstractions.NullLogger<FeeRuleCache>.Instance);
            var hubCache = new CachedHubRepository(
                new HubRepository(dbContext),
                new MemoryCache(new MemoryCacheOptions()),
                versionStore,
                options,
                Microsoft.Extensions.Logging.Abstractions.NullLogger<CachedHubRepository>.Instance);

            Assert.NotEmpty(routeCache.GetProvinceRegions());
            Assert.NotEmpty(await feeCache.GetActiveRulesAsync(MiniLogistics.Domain.Shipments.RouteType.IntraProvince));
            Assert.NotEmpty(await hubCache.GetAllAsync(activeOnly: true));
        });
    }

    private sealed class UnavailableVersionStore : IConfigurationCacheVersionStore
    {
        private static RedisConnectionException Unavailable() => new(
            ConnectionFailureType.UnableToConnect,
            "Redis unavailable in fallback test.");

        public long GetVersion(ConfigurationCacheScope scope) => throw Unavailable();

        public Task<long> GetVersionAsync(
            ConfigurationCacheScope scope,
            CancellationToken cancellationToken = default) => Task.FromException<long>(Unavailable());

        public Task<long> IncrementAsync(
            ConfigurationCacheScope scope,
            CancellationToken cancellationToken = default) => Task.FromException<long>(Unavailable());
    }
}
