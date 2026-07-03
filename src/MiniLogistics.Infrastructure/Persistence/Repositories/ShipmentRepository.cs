using Microsoft.EntityFrameworkCore;
using MiniLogistics.Application.Shipments;
using MiniLogistics.Domain.Shipments;

namespace MiniLogistics.Infrastructure.Persistence.Repositories;

public sealed class ShipmentRepository : IShipmentRepository
{
    private readonly MiniLogisticsDbContext _dbContext;

    public ShipmentRepository(MiniLogisticsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<bool> ExistsByTrackingCodeAsync(
        TrackingCode trackingCode,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.Shipments
            .AnyAsync(shipment => shipment.TrackingCode == trackingCode, cancellationToken);
    }

    public async Task<IReadOnlyList<Shipment>> GetByShopIdAsync(
        Guid shopId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Shipments
            .AsNoTracking()
            .Where(shipment => shipment.ShopId == shopId)
            .OrderByDescending(shipment => shipment.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Shipment>> GetByStatusAsync(
        ShipmentStatus status,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Shipments
            .AsNoTracking()
            .Where(shipment => shipment.Status == status)
            .OrderBy(shipment => shipment.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Shipment>> GetByStatusesAsync(
        IReadOnlyCollection<ShipmentStatus> statuses,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Shipments
            .AsNoTracking()
            .Include(shipment => shipment.Assignments)
            .Include(shipment => shipment.StatusHistory)
            .Where(shipment => statuses.Contains(shipment.Status))
            .OrderByDescending(shipment => shipment.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Shipment>> GetByIdsAsync(
        IReadOnlyCollection<Guid> shipmentIds,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Shipments
            .AsNoTracking()
            .Where(shipment => shipmentIds.Contains(shipment.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Shipment>> GetAssignedToShipperAsync(
        Guid shipperId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Shipments
            .AsNoTracking()
            .Include(shipment => shipment.Assignments)
            .Include(shipment => shipment.StatusHistory)
            .Where(shipment => shipment.Assignments.Any(assignment =>
                assignment.ShipperId == shipperId && assignment.UnassignedAtUtc == null))
            .Where(shipment => shipment.Status != ShipmentStatus.Returned
                && shipment.Status != ShipmentStatus.Cancelled)
            .OrderByDescending(shipment => shipment.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, int>> GetActiveAssignmentCountsByShipperIdsAsync(
        IReadOnlyCollection<Guid> shipperIds,
        CancellationToken cancellationToken = default)
    {
        if (shipperIds.Count == 0)
        {
            return new Dictionary<Guid, int>();
        }

        return await _dbContext.ShipmentAssignments
            .AsNoTracking()
            .Where(assignment => assignment.UnassignedAtUtc == null)
            .Where(assignment => shipperIds.Contains(assignment.ShipperId))
            .Join(
                _dbContext.Shipments.AsNoTracking(),
                assignment => assignment.ShipmentId,
                shipment => shipment.Id,
                (assignment, shipment) => new { assignment, shipment })
            .Where(row => ShipmentLoadStatuses.ActiveAssignmentStatuses.Contains(row.shipment.Status))
            .GroupBy(row => row.assignment.ShipperId)
            .Select(group => new
            {
                ShipperId = group.Key,
                Count = group.Count()
            })
            .ToDictionaryAsync(
                row => row.ShipperId,
                row => row.Count,
                cancellationToken);
    }

    public Task<Shipment?> GetByIdAndShopIdAsync(
        Guid shipmentId,
        Guid shopId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.Shipments
            .AsNoTracking()
            .Include(shipment => shipment.StatusHistory)
            .FirstOrDefaultAsync(
                shipment => shipment.Id == shipmentId && shipment.ShopId == shopId,
                cancellationToken);
    }

    public Task<Shipment?> GetTrackedByIdAndShopIdAsync(
        Guid shipmentId,
        Guid shopId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.Shipments
            .Include(shipment => shipment.Assignments)
            .Include(shipment => shipment.StatusHistory)
            .FirstOrDefaultAsync(
                shipment => shipment.Id == shipmentId && shipment.ShopId == shopId,
                cancellationToken);
    }

    public Task<Shipment?> GetTrackedByIdAsync(
        Guid shipmentId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.Shipments
            .Include(shipment => shipment.Assignments)
            .Include(shipment => shipment.StatusHistory)
            .FirstOrDefaultAsync(shipment => shipment.Id == shipmentId, cancellationToken);
    }

    public Task<Shipment?> GetByTrackingCodeAsync(
        TrackingCode trackingCode,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.Shipments
            .AsNoTracking()
            .Include(shipment => shipment.StatusHistory)
            .FirstOrDefaultAsync(shipment => shipment.TrackingCode == trackingCode, cancellationToken);
    }

    public Task<Shipment?> GetByTrackingCodeAndShopIdAsync(
        TrackingCode trackingCode,
        Guid shopId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.Shipments
            .AsNoTracking()
            .Include(shipment => shipment.StatusHistory)
            .FirstOrDefaultAsync(
                shipment => shipment.TrackingCode == trackingCode && shipment.ShopId == shopId,
                cancellationToken);
    }

    public Task<Shipment?> GetTrackedByTrackingCodeAndShopIdAsync(
        TrackingCode trackingCode,
        Guid shopId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.Shipments
            .Include(shipment => shipment.Assignments)
            .Include(shipment => shipment.StatusHistory)
            .FirstOrDefaultAsync(
                shipment => shipment.TrackingCode == trackingCode && shipment.ShopId == shopId,
                cancellationToken);
    }

    public async Task AddAsync(Shipment shipment, CancellationToken cancellationToken = default)
    {
        await _dbContext.Shipments.AddAsync(shipment, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}
