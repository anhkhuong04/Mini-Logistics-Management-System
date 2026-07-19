using Microsoft.EntityFrameworkCore;
using MiniLogistics.Application.Common;
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

    public async Task<PagedResult<Shipment>> GetByShopIdPagedAsync(
        Guid shopId,
        int pageNumber,
        int pageSize,
        ShipmentStatus? statusFilter = null,
        string? trackingCodeSearch = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedPageNumber = NormalizePageNumber(pageNumber);
        var normalizedPageSize = NormalizePageSize(pageSize);
        var query = _dbContext.Shipments
            .AsNoTracking()
            .Where(shipment => shipment.ShopId == shopId);

        if (statusFilter.HasValue)
        {
            query = query.Where(shipment => shipment.Status == statusFilter.Value);
        }

        if (!string.IsNullOrWhiteSpace(trackingCodeSearch))
        {
            var keyword = trackingCodeSearch.Trim();
            var trackingCode = new TrackingCode(keyword);
            query = query.Where(shipment =>
                shipment.TrackingCode == trackingCode
                || shipment.ReceiverName.Contains(keyword));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(shipment => shipment.CreatedAtUtc)
            .Skip((normalizedPageNumber - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Shipment>(
            items,
            normalizedPageNumber,
            normalizedPageSize,
            totalCount);
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

    public async Task<PagedResult<Shipment>> GetByStatusPagedAsync(
        ShipmentStatus status,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var normalizedPageNumber = NormalizePageNumber(pageNumber);
        var normalizedPageSize = NormalizePageSize(pageSize);
        var query = _dbContext.Shipments
            .AsNoTracking()
            .Where(shipment => shipment.Status == status);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(shipment => shipment.CreatedAtUtc)
            .Skip((normalizedPageNumber - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Shipment>(
            items,
            normalizedPageNumber,
            normalizedPageSize,
            totalCount);
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

    private static int NormalizePageNumber(int pageNumber)
    {
        return Math.Max(1, pageNumber);
    }

    private static int NormalizePageSize(int pageSize)
    {
        return Math.Clamp(pageSize, 1, 100);
    }
}
