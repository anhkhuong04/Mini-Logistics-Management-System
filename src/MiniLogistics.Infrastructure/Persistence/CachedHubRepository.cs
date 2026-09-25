using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MiniLogistics.Application.Shippers;
using MiniLogistics.Domain.Operations;
using MiniLogistics.Infrastructure.Persistence.Repositories;

namespace MiniLogistics.Infrastructure.Persistence;

public sealed class CachedHubRepository : IHubRepository, IHubCacheInvalidator
{
    private const string AllHubsCacheKey = "hubs_all";
    private const string ActiveHubsCacheKey = "hubs_active";

    private readonly HubRepository _repository;
    private readonly IMemoryCache _cache;
    private readonly IConfigurationCacheVersionStore _versionStore;
    private readonly ConfigurationCacheOptions _cacheOptions;
    private readonly ILogger<CachedHubRepository> _logger;

    public CachedHubRepository(
        HubRepository repository,
        IMemoryCache cache,
        IConfigurationCacheVersionStore versionStore,
        IOptions<ConfigurationCacheOptions> cacheOptions,
        ILogger<CachedHubRepository> logger)
    {
        _repository = repository;
        _cache = cache;
        _versionStore = versionStore;
        _cacheOptions = cacheOptions.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Hub>> GetAllAsync(
        bool activeOnly = false,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = activeOnly ? ActiveHubsCacheKey : AllHubsCacheKey;
        long version;
        try
        {
            version = await _versionStore.GetVersionAsync(ConfigurationCacheScope.Hubs, cancellationToken);
        }
        catch (Exception exception) when (ConfigurationCacheStoreFailure.IsUnavailable(exception))
        {
            _logger.LogWarning(exception, "Configuration version store unavailable; loading hubs from SQL.");
            return await _repository.GetAllAsync(activeOnly, cancellationToken);
        }

        var cached = _cache.Get<VersionedConfigurationCacheEntry<IReadOnlyList<Hub>>>(cacheKey);
        if (cached?.Version == version)
        {
            return cached.Value;
        }

        var hubs = await _repository.GetAllAsync(activeOnly, cancellationToken);
        _cache.Set(
            cacheKey,
            new VersionedConfigurationCacheEntry<IReadOnlyList<Hub>>(version, hubs),
            TimeSpan.FromSeconds(_cacheOptions.ConsistencyWindowSeconds));
        return hubs;
    }

    public Task<IReadOnlyList<Hub>> GetByIdsAsync(
        IReadOnlyCollection<Guid> hubIds,
        CancellationToken cancellationToken = default) =>
        _repository.GetByIdsAsync(hubIds, cancellationToken);

    public Task<Hub?> GetByIdAsync(Guid hubId, CancellationToken cancellationToken = default) =>
        _repository.GetByIdAsync(hubId, cancellationToken);

    public Task<Hub?> GetByCodeAsync(string code, CancellationToken cancellationToken = default) =>
        _repository.GetByCodeAsync(code, cancellationToken);

    public Task AddAsync(Hub hub, CancellationToken cancellationToken = default) =>
        _repository.AddAsync(hub, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _repository.SaveChangesAsync(cancellationToken);

    public async Task InvalidateAsync(CancellationToken cancellationToken = default)
    {
        InvalidateLocal();
        try
        {
            await _versionStore.IncrementAsync(ConfigurationCacheScope.Hubs, cancellationToken);
        }
        catch (Exception exception) when (ConfigurationCacheStoreFailure.IsUnavailable(exception))
        {
            _logger.LogError(exception,
                "Could not publish hub invalidation. Other nodes will refresh within {ConsistencyWindowSeconds} seconds.",
                _cacheOptions.ConsistencyWindowSeconds);
        }
    }

    private void InvalidateLocal()
    {
        _cache.Remove(AllHubsCacheKey);
        _cache.Remove(ActiveHubsCacheKey);
    }
}
