using Microsoft.Extensions.Caching.Memory;
using MiniLogistics.Application.Fees;
using MiniLogistics.Domain.Fees;
using MiniLogistics.Domain.Shipments;
using MiniLogistics.Infrastructure.Persistence.Repositories;

namespace MiniLogistics.Infrastructure.Persistence;

public sealed class FeeRuleCache : IFeeRuleCache
{
    private const string CacheKeyPrefix = "fee_rules_active_";
    private static readonly TimeSpan Expiry = TimeSpan.FromMinutes(30);

    private readonly FeeRuleRepository _repository;
    private readonly IMemoryCache _cache;

    public FeeRuleCache(FeeRuleRepository repository, IMemoryCache cache)
    {
        _repository = repository;
        _cache = cache;
    }

    public async Task<IReadOnlyCollection<FeeRule>> GetActiveRulesAsync(
        RouteType routeType,
        CancellationToken cancellationToken = default)
    {
        return await _cache.GetOrCreateAsync(
            GetCacheKey(routeType),
            async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = Expiry;
                return await _repository.GetActiveRulesAsync(routeType, cancellationToken);
            }) ?? [];
    }

    public void Invalidate()
    {
        foreach (var routeType in Enum.GetValues<RouteType>())
        {
            _cache.Remove(GetCacheKey(routeType));
        }
    }

    private static string GetCacheKey(RouteType routeType)
    {
        return $"{CacheKeyPrefix}{routeType}";
    }
}
