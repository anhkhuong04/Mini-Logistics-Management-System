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
        return GetProvinceRegionSnapshot()
            .ToDictionary(
                config => config.Province,
                config => config.Region,
                StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<RouteRegionSourceEntry> GetProvinceRegionSnapshot()
    {
        var activeConfigs = _dbContext.RouteRegionConfigs
            .AsNoTracking()
            .Where(config => config.IsActive)
            .OrderBy(config => config.Province)
            .ToList();

        if (activeConfigs.Count == 0)
        {
            return DefaultRouteRegionConfigSource.Instance.GetProvinceRegionSnapshot();
        }

        return activeConfigs
            .Select(config => new RouteRegionSourceEntry(
                config.Province,
                config.Region,
                config.Id,
                config.Version))
            .ToList();
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

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (ConfigurationIndexConflict.IsConflict(exception))
        {
            _dbContext.ChangeTracker.Clear();
            throw ConfigurationIndexConflict.ToException(exception);
        }
    }
}
