using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MiniLogistics.Application.Routing;
using MiniLogistics.Domain.Operations;
using MiniLogistics.Infrastructure.Persistence.Repositories;

namespace MiniLogistics.Infrastructure.Persistence;

public sealed class CachedRouteRegionConfigRepository : IRouteRegionConfigRepository, IRouteRegionConfigCacheInvalidator
{
    private const string ProvinceRegionsCacheKey = "route_region_configs_province_regions";
    private const string ProvinceRegionSnapshotCacheKey = "route_region_configs_province_region_snapshot";
    private const string AllConfigsCacheKey = "route_region_configs_all";
    private const string ActiveConfigsCacheKey = "route_region_configs_active";

    private readonly RouteRegionConfigRepository _repository;
    private readonly IMemoryCache _cache;
    private readonly IConfigurationCacheVersionStore _versionStore;
    private readonly ConfigurationCacheOptions _cacheOptions;
    private readonly ILogger<CachedRouteRegionConfigRepository> _logger;

    public CachedRouteRegionConfigRepository(
        RouteRegionConfigRepository repository,
        IMemoryCache cache,
        IConfigurationCacheVersionStore versionStore,
        IOptions<ConfigurationCacheOptions> cacheOptions,
        ILogger<CachedRouteRegionConfigRepository> logger)
    {
        _repository = repository;
        _cache = cache;
        _versionStore = versionStore;
        _cacheOptions = cacheOptions.Value;
        _logger = logger;
    }

    public IReadOnlyDictionary<string, string> GetProvinceRegions()
    {
        if (!TryGetVersion(out var version))
        {
            return _repository.GetProvinceRegions();
        }

        var cached = _cache.Get<VersionedConfigurationCacheEntry<IReadOnlyDictionary<string, string>>>(
            ProvinceRegionsCacheKey);
        if (cached?.Version == version)
        {
            return cached.Value;
        }

        var regions = _repository.GetProvinceRegions();
        SetCache(ProvinceRegionsCacheKey, version, regions);
        return regions;
    }

    public IReadOnlyList<RouteRegionSourceEntry> GetProvinceRegionSnapshot()
    {
        if (!TryGetVersion(out var version))
        {
            return _repository.GetProvinceRegionSnapshot();
        }

        var cached = _cache.Get<VersionedConfigurationCacheEntry<IReadOnlyList<RouteRegionSourceEntry>>>(
            ProvinceRegionSnapshotCacheKey);
        if (cached?.Version == version)
        {
            return cached.Value;
        }

        var snapshot = _repository.GetProvinceRegionSnapshot();
        SetCache(ProvinceRegionSnapshotCacheKey, version, snapshot);
        return snapshot;
    }

    public async Task<IReadOnlyList<RouteRegionConfig>> GetAllAsync(
        bool activeOnly = false,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = activeOnly ? ActiveConfigsCacheKey : AllConfigsCacheKey;
        long version;
        try
        {
            version = await _versionStore.GetVersionAsync(ConfigurationCacheScope.RouteRegions, cancellationToken);
        }
        catch (Exception exception) when (ConfigurationCacheStoreFailure.IsUnavailable(exception))
        {
            _logger.LogWarning(exception, "Configuration version store unavailable; loading route regions from SQL.");
            return await _repository.GetAllAsync(activeOnly, cancellationToken);
        }

        var cached = _cache.Get<VersionedConfigurationCacheEntry<IReadOnlyList<RouteRegionConfig>>>(cacheKey);
        if (cached?.Version == version)
        {
            return cached.Value;
        }

        var configs = await _repository.GetAllAsync(activeOnly, cancellationToken);
        SetCache(cacheKey, version, configs);
        return configs;
    }

    public Task<IReadOnlyList<RouteRegionConfig>> GetActiveByProvinceAsync(
        string province,
        CancellationToken cancellationToken = default) =>
        _repository.GetActiveByProvinceAsync(province, cancellationToken);

    public Task<int> GetLatestVersionAsync(
        string province,
        CancellationToken cancellationToken = default) =>
        _repository.GetLatestVersionAsync(province, cancellationToken);

    public Task AddAsync(RouteRegionConfig config, CancellationToken cancellationToken = default) =>
        _repository.AddAsync(config, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _repository.SaveChangesAsync(cancellationToken);

    public async Task InvalidateAsync(CancellationToken cancellationToken = default)
    {
        InvalidateLocal();
        try
        {
            await _versionStore.IncrementAsync(ConfigurationCacheScope.RouteRegions, cancellationToken);
        }
        catch (Exception exception) when (ConfigurationCacheStoreFailure.IsUnavailable(exception))
        {
            _logger.LogError(exception,
                "Could not publish route-region invalidation. Other nodes will refresh within {ConsistencyWindowSeconds} seconds.",
                _cacheOptions.ConsistencyWindowSeconds);
        }
    }

    private bool TryGetVersion(out long version)
    {
        try
        {
            version = _versionStore.GetVersion(ConfigurationCacheScope.RouteRegions);
            return true;
        }
        catch (Exception exception) when (ConfigurationCacheStoreFailure.IsUnavailable(exception))
        {
            _logger.LogWarning(exception, "Configuration version store unavailable; loading route regions from SQL.");
            version = 0;
            return false;
        }
    }

    private void SetCache<T>(string key, long version, T value) =>
        _cache.Set(
            key,
            new VersionedConfigurationCacheEntry<T>(version, value),
            TimeSpan.FromSeconds(_cacheOptions.ConsistencyWindowSeconds));

    private void InvalidateLocal()
    {
        _cache.Remove(ProvinceRegionsCacheKey);
        _cache.Remove(ProvinceRegionSnapshotCacheKey);
        _cache.Remove(AllConfigsCacheKey);
        _cache.Remove(ActiveConfigsCacheKey);
    }
}
