using Microsoft.EntityFrameworkCore;
using MiniLogistics.Application.Shippers;
using MiniLogistics.Domain.Operations;

namespace MiniLogistics.Infrastructure.Persistence.Repositories;

public sealed class HubRepository : IHubRepository
{
    private readonly MiniLogisticsDbContext _dbContext;

    public HubRepository(MiniLogisticsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<Hub>> GetAllAsync(
        bool activeOnly = false,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Hubs.AsNoTracking();

        if (activeOnly)
        {
            query = query.Where(hub => hub.IsActive);
        }

        return await query
            .OrderByDescending(hub => hub.IsRegionalSortingHub)
            .ThenBy(hub => hub.Province)
            .ThenBy(hub => hub.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Hub>> GetByIdsAsync(
        IReadOnlyCollection<Guid> hubIds,
        CancellationToken cancellationToken = default)
    {
        if (hubIds.Count == 0)
        {
            return [];
        }

        return await _dbContext.Hubs
            .AsNoTracking()
            .Where(hub => hubIds.Contains(hub.Id))
            .ToListAsync(cancellationToken);
    }

    public Task<Hub?> GetByCodeAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        var normalizedCode = code.Trim().ToUpperInvariant();

        return _dbContext.Hubs
            .FirstOrDefaultAsync(hub => hub.Code == normalizedCode, cancellationToken);
    }

    public async Task AddAsync(Hub hub, CancellationToken cancellationToken = default)
    {
        await _dbContext.Hubs.AddAsync(hub, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}
