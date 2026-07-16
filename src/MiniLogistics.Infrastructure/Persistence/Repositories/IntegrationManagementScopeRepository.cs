using Microsoft.EntityFrameworkCore;
using MiniLogistics.Application.PartnerApi;
using MiniLogistics.Domain.PartnerApi;

namespace MiniLogistics.Infrastructure.Persistence.Repositories;

public sealed class IntegrationManagementScopeRepository : IIntegrationManagementScopeRepository
{
    private readonly MiniLogisticsDbContext _dbContext;

    public IntegrationManagementScopeRepository(MiniLogisticsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<bool> AnyActiveScopeAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.IntegrationManagementScopes
            .AsNoTracking()
            .AnyAsync(scope => scope.IsActive, cancellationToken);
    }

    public async Task<IReadOnlyList<IntegrationManagementScope>> GetActiveByActorUserIdAsync(
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.IntegrationManagementScopes
            .AsNoTracking()
            .Where(scope => scope.ActorUserId == actorUserId && scope.IsActive)
            .ToListAsync(cancellationToken);
    }
}
