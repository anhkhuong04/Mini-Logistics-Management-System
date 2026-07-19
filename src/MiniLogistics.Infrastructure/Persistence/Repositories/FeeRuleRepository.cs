using Microsoft.EntityFrameworkCore;
using MiniLogistics.Application.Fees;
using MiniLogistics.Domain.Fees;
using MiniLogistics.Domain.Shipments;

namespace MiniLogistics.Infrastructure.Persistence.Repositories;

public sealed class FeeRuleRepository : IFeeRuleRepository
{
    private readonly MiniLogisticsDbContext _dbContext;

    public FeeRuleRepository(MiniLogisticsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<FeeRule>> GetActiveRulesAsync(
        RouteType routeType,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.FeeRules
            .AsNoTracking()
            .Where(rule => rule.IsActive && rule.RouteType == routeType)
            .OrderBy(rule => rule.MinimumWeightKg)
            .ToListAsync(cancellationToken);
    }
}
