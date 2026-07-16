using Microsoft.EntityFrameworkCore;
using MiniLogistics.Application.Fees;
using MiniLogistics.Domain.Fees;
using MiniLogistics.Domain.Shipments;

namespace MiniLogistics.Infrastructure.Persistence.Repositories;

public sealed class FeeConfigurationRepository : IFeeConfigurationRepository
{
    private readonly MiniLogisticsDbContext _dbContext;

    public FeeConfigurationRepository(MiniLogisticsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<FeeRule>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.FeeRules
            .AsNoTracking()
            .OrderBy(rule => rule.RouteType)
            .ThenByDescending(rule => rule.Version)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<FeeRule>> GetActiveRulesForUpdateAsync(
        RouteType routeType,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.FeeRules
            .Where(rule => rule.IsActive && rule.RouteType == routeType)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetLatestVersionAsync(
        RouteType routeType,
        CancellationToken cancellationToken = default)
    {
        var latestVersion = await _dbContext.FeeRules
            .AsNoTracking()
            .Where(rule => rule.RouteType == routeType)
            .Select(rule => (int?)rule.Version)
            .MaxAsync(cancellationToken);

        return latestVersion ?? 0;
    }

    public async Task AddAsync(FeeRule feeRule, CancellationToken cancellationToken = default)
    {
        await _dbContext.FeeRules.AddAsync(feeRule, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}
