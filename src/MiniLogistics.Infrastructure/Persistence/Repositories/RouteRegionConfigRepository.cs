using Microsoft.EntityFrameworkCore;
using MiniLogistics.Application.Routing;
using MiniLogistics.Domain.Operations;

namespace MiniLogistics.Infrastructure.Persistence.Repositories;

public sealed class RouteRegionConfigRepository : IRouteRegionConfigRepository
{
    private readonly MiniLogisticsDbContext _dbContext;

    public RouteRegionConfigRepository(MiniLogisticsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public IReadOnlyDictionary<string, string> GetProvinceRegions()
    {
        var activeConfigs = _dbContext.RouteRegionConfigs
            .AsNoTracking()
            .Where(config => config.IsActive)
            .OrderBy(config => config.Province)
            .ToList();

        if (activeConfigs.Count == 0)
        {
            return DefaultRouteRegionConfigSource.Instance.GetProvinceRegions();
        }

        return activeConfigs
            .GroupBy(config => config.Province, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(config => config.Version).First().Region,
                StringComparer.OrdinalIgnoreCase);
    }

    public async Task<IReadOnlyList<RouteRegionConfig>> GetAllAsync(
        bool activeOnly = false,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.RouteRegionConfigs.AsNoTracking();
        if (activeOnly)
        {
            query = query.Where(config => config.IsActive);
        }

        return await query
            .OrderBy(config => config.Province)
            .ThenByDescending(config => config.Version)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RouteRegionConfig>> GetActiveByProvinceAsync(
        string province,
        CancellationToken cancellationToken = default)
    {
        var normalizedProvince = province.Trim();
        return await _dbContext.RouteRegionConfigs
            .Where(config => config.IsActive && config.Province == normalizedProvince)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetLatestVersionAsync(
        string province,
        CancellationToken cancellationToken = default)
    {
        var normalizedProvince = province.Trim();
        var latestVersion = await _dbContext.RouteRegionConfigs
            .AsNoTracking()
            .Where(config => config.Province == normalizedProvince)
            .Select(config => (int?)config.Version)
            .MaxAsync(cancellationToken);

        return latestVersion ?? 0;
    }

    public async Task AddAsync(
        RouteRegionConfig config,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.RouteRegionConfigs.AddAsync(config, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}
