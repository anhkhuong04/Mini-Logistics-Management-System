using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MiniLogistics.Application.Fees;
using MiniLogistics.Domain.Fees;
using MiniLogistics.Domain.Shipments;
using MiniLogistics.Infrastructure.Persistence.Repositories;

namespace MiniLogistics.Infrastructure.Persistence;

public sealed class FeeRuleCache : IFeeRuleCache
{
    private const string CacheKeyPrefix = "fee_rules_active_";

    private readonly FeeRuleRepository _repository;
    private readonly IMemoryCache _cache;
    private readonly IConfigurationCacheVersionStore _versionStore;
    private readonly ConfigurationCacheOptions _cacheOptions;
    private readonly ILogger<FeeRuleCache> _logger;

    public FeeRuleCache(
        FeeRuleRepository repository,
        IMemoryCache cache,
        IConfigurationCacheVersionStore versionStore,
        IOptions<ConfigurationCacheOptions> cacheOptions,
        ILogger<FeeRuleCache> logger)
    {
        _repository = repository;
        _cache = cache;
        _versionStore = versionStore;
        _cacheOptions = cacheOptions.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyCollection<FeeRule>> GetActiveRulesAsync(
        RouteType routeType,
        CancellationToken cancellationToken = default)
    {
        long version;
        try
        {
            version = await _versionStore.GetVersionAsync(ConfigurationCacheScope.FeeRules, cancellationToken);
        }
        catch (Exception exception) when (ConfigurationCacheStoreFailure.IsUnavailable(exception))
        {
            _logger.LogWarning(exception, "Configuration version store unavailable; loading fee rules from SQL.");
            return await _repository.GetActiveRulesAsync(routeType, cancellationToken);
        }

        var cacheKey = GetCacheKey(routeType);
        var cached = _cache.Get<VersionedConfigurationCacheEntry<IReadOnlyCollection<FeeRule>>>(cacheKey);
        if (cached?.Version == version)
        {
            return cached.Value;
        }

        var rules = await _repository.GetActiveRulesAsync(routeType, cancellationToken);
        _cache.Set(
            cacheKey,
            new VersionedConfigurationCacheEntry<IReadOnlyCollection<FeeRule>>(version, rules),
            TimeSpan.FromSeconds(_cacheOptions.ConsistencyWindowSeconds));
        return rules;
    }

    public async Task InvalidateAsync(CancellationToken cancellationToken = default)
    {
        RemoveLocalEntries();
        try
        {
            await _versionStore.IncrementAsync(ConfigurationCacheScope.FeeRules, cancellationToken);
        }
        catch (Exception exception) when (ConfigurationCacheStoreFailure.IsUnavailable(exception))
        {
            _logger.LogError(exception,
                "Could not publish fee-rule invalidation. Other nodes will refresh within {ConsistencyWindowSeconds} seconds.",
                _cacheOptions.ConsistencyWindowSeconds);
        }
    }

    private void RemoveLocalEntries()
    {
        foreach (var routeType in Enum.GetValues<RouteType>())
        {
            _cache.Remove(GetCacheKey(routeType));
        }
    }

    private static string GetCacheKey(RouteType routeType) => $"{CacheKeyPrefix}{routeType}";
}
