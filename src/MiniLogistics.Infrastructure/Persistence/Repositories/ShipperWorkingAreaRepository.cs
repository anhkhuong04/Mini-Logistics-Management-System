using Microsoft.EntityFrameworkCore;
using MiniLogistics.Application.Shippers;
using MiniLogistics.Domain.Operations;

namespace MiniLogistics.Infrastructure.Persistence.Repositories;

public sealed class ShipperWorkingAreaRepository : IShipperWorkingAreaRepository
{
    private readonly MiniLogisticsDbContext _dbContext;

    public ShipperWorkingAreaRepository(MiniLogisticsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<ShipperWorkingArea>> GetByShipperIdAsync(
        Guid shipperId,
        bool activeOnly = false,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.ShipperWorkingAreas
            .Where(area => area.ShipperId == shipperId);

        if (activeOnly)
        {
            query = query.Where(area => area.IsActive);
        }

        return await query
            .OrderBy(area => area.Province)
            .ThenBy(area => area.Ward)
            .ThenBy(area => area.ZoneCode)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ShipperWorkingArea>> GetActiveByShipperIdsAsync(
        IReadOnlyCollection<Guid> shipperIds,
        CancellationToken cancellationToken = default)
    {
        if (shipperIds.Count == 0)
        {
            return [];
        }

        return await _dbContext.ShipperWorkingAreas
            .AsNoTracking()
            .Where(area => area.IsActive && shipperIds.Contains(area.ShipperId))
            .OrderBy(area => area.Province)
            .ThenBy(area => area.Ward)
            .ThenBy(area => area.ZoneCode)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ShipperWorkingArea>> GetActiveByHubOrProvinceAsync(
        Guid? hubId,
        string province,
        string? ward = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedProvince = province.Trim();
        var normalizedWard = string.IsNullOrWhiteSpace(ward)
            ? null
            : ward.Trim();

        var query = _dbContext.ShipperWorkingAreas
            .AsNoTracking()
            .Where(area => area.IsActive)
            .Where(area => area.Province == normalizedProvince || (hubId.HasValue && area.HubId == hubId.Value));

        if (!string.IsNullOrWhiteSpace(normalizedWard))
        {
            query = query.Where(area => area.Ward == null || area.Ward == normalizedWard);
        }

        return await query
            .OrderBy(area => area.Province)
            .ThenBy(area => area.Ward)
            .ThenBy(area => area.ZoneCode)
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountActiveByHubIdAsync(
        Guid hubId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.ShipperWorkingAreas
            .AsNoTracking()
            .CountAsync(area => area.IsActive && area.HubId == hubId, cancellationToken);
    }

    public async Task AddAsync(
        ShipperWorkingArea workingArea,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.ShipperWorkingAreas.AddAsync(workingArea, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}
