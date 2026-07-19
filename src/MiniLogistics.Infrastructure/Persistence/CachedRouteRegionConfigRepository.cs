using Microsoft.Extensions.Caching.Memory;
using MiniLogistics.Application.Routing;
using MiniLogistics.Domain.Operations;
using MiniLogistics.Infrastructure.Persistence.Repositories;

namespace MiniLogistics.Infrastructure.Persistence;

public sealed class CachedRouteRegionConfigRepository : IRouteRegionConfigRepository
{
    private const string ProvinceRegionsCacheKey = "route_region_configs_province_regions";
    private const string AllConfigsCacheKey = "route_region_configs_all";
    private const string ActiveConfigsCacheKey = "route_region_configs_active";
    private static readonly TimeSpan Expiry = TimeSpan.FromMinutes(60);

    private readonly RouteRegionConfigRepository _repository;
    private readonly IMemoryCache _cache;

    public CachedRouteRegionConfigRepository(
        RouteRegionConfigRepository repository,
        IMemoryCache cache)
    {
        _repository = repository;
        _cache = cache;
    }

    public IReadOnlyDictionary<string, string> GetProvinceRegions()
    {
        return _cache.GetOrCreate(
            ProvinceRegionsCacheKey,
            entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = Expiry;
                return _repository.GetProvinceRegions();
            }) ?? DefaultRouteRegionConfigSource.Instance.GetProvinceRegions();
    }

    public async Task<IReadOnlyList<RouteRegionConfig>> GetAllAsync(
        bool activeOnly = false,
        CancellationToken cancellationToken = default)
    {
        return await _cache.GetOrCreateAsync(
            activeOnly ? ActiveConfigsCacheKey : AllConfigsCacheKey,
            async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = Expiry;
                return await _repository.GetAllAsync(activeOnly, cancellationToken);
            }) ?? [];
    }

    public Task<IReadOnlyList<RouteRegionConfig>> GetActiveByProvinceAsync(
        string province,
        CancellationToken cancellationToken = default)
    {
        return _repository.GetActiveByProvinceAsync(province, cancellationToken);
    }

    public Task<int> GetLatestVersionAsync(
        string province,
        CancellationToken cancellationToken = default)
    {
        return _repository.GetLatestVersionAsync(province, cancellationToken);
    }

    public async Task AddAsync(
        RouteRegionConfig config,
        CancellationToken cancellationToken = default)
    {
        Invalidate();
        await _repository.AddAsync(config, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _repository.SaveChangesAsync(cancellationToken);
        Invalidate();
    }

    private void Invalidate()
    {
        _cache.Remove(ProvinceRegionsCacheKey);
        _cache.Remove(AllConfigsCacheKey);
        _cache.Remove(ActiveConfigsCacheKey);
    }
}
