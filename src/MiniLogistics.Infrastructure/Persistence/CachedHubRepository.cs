using Microsoft.Extensions.Caching.Memory;
using MiniLogistics.Application.Shippers;
using MiniLogistics.Domain.Operations;
using MiniLogistics.Infrastructure.Persistence.Repositories;

namespace MiniLogistics.Infrastructure.Persistence;

public sealed class CachedHubRepository : IHubRepository
{
    private const string AllHubsCacheKey = "hubs_all";
    private const string ActiveHubsCacheKey = "hubs_active";
    private static readonly TimeSpan Expiry = TimeSpan.FromMinutes(30);

    private readonly HubRepository _repository;
    private readonly IMemoryCache _cache;

    public CachedHubRepository(HubRepository repository, IMemoryCache cache)
    {
        _repository = repository;
        _cache = cache;
    }

    public async Task<IReadOnlyList<Hub>> GetAllAsync(
        bool activeOnly = false,
        CancellationToken cancellationToken = default)
    {
        return await _cache.GetOrCreateAsync(
            activeOnly ? ActiveHubsCacheKey : AllHubsCacheKey,
            async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = Expiry;
                return await _repository.GetAllAsync(activeOnly, cancellationToken);
            }) ?? [];
    }

    public Task<IReadOnlyList<Hub>> GetByIdsAsync(
        IReadOnlyCollection<Guid> hubIds,
        CancellationToken cancellationToken = default)
    {
        return _repository.GetByIdsAsync(hubIds, cancellationToken);
    }

    public Task<Hub?> GetByIdAsync(Guid hubId, CancellationToken cancellationToken = default)
    {
        return _repository.GetByIdAsync(hubId, cancellationToken);
    }

    public Task<Hub?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        return _repository.GetByCodeAsync(code, cancellationToken);
    }

    public async Task AddAsync(Hub hub, CancellationToken cancellationToken = default)
    {
        Invalidate();
        await _repository.AddAsync(hub, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _repository.SaveChangesAsync(cancellationToken);
        Invalidate();
    }

    private void Invalidate()
    {
        _cache.Remove(AllHubsCacheKey);
        _cache.Remove(ActiveHubsCacheKey);
    }
}
