using Microsoft.EntityFrameworkCore;
using MiniLogistics.Application.Common;
using MiniLogistics.Application.Shipments;
using MiniLogistics.Domain.CashOnDelivery;
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

    public async Task<PagedResult<Shipment>> SearchByShopAsync(
        ShopShipmentSearchCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        var normalizedPageNumber = NormalizePageNumber(criteria.PageNumber);
        var normalizedPageSize = NormalizePageSize(criteria.PageSize);
        var query = _dbContext.Shipments
            .AsNoTracking()
            .Where(shipment => shipment.ShopId == criteria.ShopId);

        if (criteria.StatusFilter.HasValue)
        {
            query = query.Where(shipment => shipment.Status == criteria.StatusFilter.Value);
        }

        query = ApplyShipmentTextFilters(query, criteria.TrackingCodeSearch, includeReceiverPhone: false);

        if (!string.IsNullOrWhiteSpace(criteria.ReceiverNameSearch))
        {
            var receiverName = criteria.ReceiverNameSearch.Trim();
            query = query.Where(shipment => shipment.ReceiverName.Contains(receiverName));
        }

        if (!string.IsNullOrWhiteSpace(criteria.ReceiverPhoneSearch))
        {
            var receiverPhone = criteria.ReceiverPhoneSearch.Trim();
            query = query.Where(shipment => shipment.ReceiverPhone.Value.Contains(receiverPhone));
        }

        query = ApplyCreatedAtRange(query, criteria.FromUtc, criteria.ToUtc);
        query = ApplyCodAmountRange(query, criteria.MinCodAmount, criteria.MaxCodAmount);

        query = (criteria.SortBy, criteria.SortDirection) switch
        {
            (ShopShipmentSortBy.TrackingCode, SortDirection.Ascending) => query.OrderBy(shipment => shipment.TrackingCode),
            (ShopShipmentSortBy.TrackingCode, SortDirection.Descending) => query.OrderByDescending(shipment => shipment.TrackingCode),
            (ShopShipmentSortBy.ReceiverName, SortDirection.Ascending) => query.OrderBy(shipment => shipment.ReceiverName),
            (ShopShipmentSortBy.ReceiverName, SortDirection.Descending) => query.OrderByDescending(shipment => shipment.ReceiverName),
            (ShopShipmentSortBy.CodAmount, SortDirection.Ascending) => query.OrderBy(shipment => shipment.CodAmount.Amount),
            (ShopShipmentSortBy.CodAmount, SortDirection.Descending) => query.OrderByDescending(shipment => shipment.CodAmount.Amount),
            (ShopShipmentSortBy.ShippingFee, SortDirection.Ascending) => query.OrderBy(shipment => shipment.ShippingFee.Amount),
            (ShopShipmentSortBy.ShippingFee, SortDirection.Descending) => query.OrderByDescending(shipment => shipment.ShippingFee.Amount),
            (_, SortDirection.Ascending) => query.OrderBy(shipment => shipment.CreatedAtUtc),
            _ => query.OrderByDescending(shipment => shipment.CreatedAtUtc)
        };

        return await ToPagedResultAsync(query, normalizedPageNumber, normalizedPageSize, cancellationToken);
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

    public async Task<PagedResult<Shipment>> SearchPendingPickupAsync(
        PendingPickupShipmentSearchCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        var normalizedPageNumber = NormalizePageNumber(criteria.PageNumber);
        var normalizedPageSize = NormalizePageSize(criteria.PageSize);
        var query = _dbContext.Shipments
            .AsNoTracking()
            .Where(shipment => shipment.Status == ShipmentStatus.PendingPickup);

        query = ApplyShipmentTextFilters(query, criteria.SearchText, includeReceiverPhone: false);
        query = ApplyProvinceFilter(query, criteria.Province, pickupOnly: true);
        query = ApplyCreatedAtRange(query, criteria.FromUtc, criteria.ToUtc);
        query = ApplyCodAmountRange(query, criteria.MinCodAmount, criteria.MaxCodAmount);

        if (criteria.SlaCutoffUtc.HasValue)
        {
            query = query.Where(shipment => shipment.CreatedAtUtc <= criteria.SlaCutoffUtc.Value);
        }

        return await ToPagedResultAsync(
            query.OrderBy(shipment => shipment.CreatedAtUtc),
            normalizedPageNumber,
            normalizedPageSize,
            cancellationToken);
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

    public async Task<PagedResult<Shipment>> SearchOperationsAsync(
        OperationsShipmentSearchCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        var normalizedPageNumber = NormalizePageNumber(criteria.PageNumber);
        var normalizedPageSize = NormalizePageSize(criteria.PageSize);
        var statuses = criteria.Statuses.Count == 0
            ? Array.Empty<ShipmentStatus>()
            : criteria.Statuses;
        var query = _dbContext.Shipments
            .AsNoTracking()
            .Where(shipment => statuses.Contains(shipment.Status));

        query = query.Where(shipment =>
            shipment.Status != ShipmentStatus.Delivered
            || _dbContext.CodTransactions.Any(codTransaction =>
                codTransaction.ShipmentId == shipment.Id
                && codTransaction.Status == CodStatus.PendingCollection));

        if (criteria.CodStatus.HasValue)
        {
            var codStatus = criteria.CodStatus.Value;
            query = codStatus == CodStatus.NotRequired
                ? query.Where(shipment => !_dbContext.CodTransactions.Any(codTransaction =>
                    codTransaction.ShipmentId == shipment.Id))
                : query.Where(shipment => _dbContext.CodTransactions.Any(codTransaction =>
                    codTransaction.ShipmentId == shipment.Id
                    && codTransaction.Status == codStatus));
        }

        if (criteria.ShipperId.HasValue)
        {
            query = query.Where(shipment => shipment.Assignments.Any(assignment =>
                assignment.ShipperId == criteria.ShipperId.Value
                && assignment.UnassignedAtUtc == null));
        }

        query = ApplyShipmentTextFilters(query, criteria.SearchText, includeReceiverPhone: true);
        query = ApplyProvinceFilter(query, criteria.Province, pickupOnly: false);
        query = ApplyCreatedAtRange(query, criteria.FromUtc, criteria.ToUtc);
        query = ApplyCodAmountRange(query, criteria.MinCodAmount, criteria.MaxCodAmount);

        if (criteria.SlaOnly)
        {
            var codSlaCutoffUtc = criteria.SlaReferenceUtc.AddHours(-24);
            query = query.Where(shipment =>
                shipment.Status == ShipmentStatus.DeliveryFailed
                    && _dbContext.ShipmentStatusHistories.Count(history =>
                        history.ShipmentId == shipment.Id
                        && history.Status == ShipmentStatus.DeliveryFailed) > 1
                || shipment.Status == ShipmentStatus.Delivered
                    && _dbContext.CodTransactions.Any(codTransaction =>
                        codTransaction.ShipmentId == shipment.Id
                        && codTransaction.Status == CodStatus.PendingCollection)
                    && (_dbContext.ShipmentStatusHistories
                        .Where(history =>
                            history.ShipmentId == shipment.Id
                            && history.Status == ShipmentStatus.Delivered)
                        .OrderByDescending(history => history.ChangedAtUtc)
                        .Select(history => (DateTimeOffset?)history.ChangedAtUtc)
                        .FirstOrDefault() ?? shipment.CreatedAtUtc) <= codSlaCutoffUtc);
        }

        return await ToPagedResultAsync(
            query
                .Include(shipment => shipment.Assignments)
                .Include(shipment => shipment.StatusHistory)
                .OrderByDescending(shipment => shipment.CreatedAtUtc),
            normalizedPageNumber,
            normalizedPageSize,
            cancellationToken);
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

    public async Task<PagedResult<Shipment>> SearchAssignedToShipperAsync(
        AssignedShipmentsForShipperSearchCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        var normalizedPageNumber = NormalizePageNumber(criteria.PageNumber);
        var normalizedPageSize = NormalizePageSize(criteria.PageSize);
        var query = _dbContext.Shipments
            .AsNoTracking()
            .Where(shipment => shipment.Assignments.Any(assignment =>
                assignment.ShipperId == criteria.ShipperId
                && assignment.UnassignedAtUtc == null))
            .Where(shipment => shipment.Status != ShipmentStatus.Returned
                && shipment.Status != ShipmentStatus.Cancelled)
            .Where(shipment =>
                shipment.Status != ShipmentStatus.Delivered
                || _dbContext.CodTransactions.Any(codTransaction =>
                    codTransaction.ShipmentId == shipment.Id
                    && codTransaction.Status == CodStatus.PendingCollection));

        if (criteria.Statuses is { Count: > 0 })
        {
            query = query.Where(shipment => criteria.Statuses.Contains(shipment.Status));
        }

        if (criteria.CodStatus.HasValue)
        {
            var codStatus = criteria.CodStatus.Value;
            query = codStatus == CodStatus.NotRequired
                ? query.Where(shipment => !_dbContext.CodTransactions.Any(codTransaction =>
                    codTransaction.ShipmentId == shipment.Id))
                : query.Where(shipment => _dbContext.CodTransactions.Any(codTransaction =>
                    codTransaction.ShipmentId == shipment.Id
                    && codTransaction.Status == codStatus));
        }

        query = ApplyShipmentTextFilters(query, criteria.SearchText, includeReceiverPhone: true);

        return await ToPagedResultAsync(
            query
                .Include(shipment => shipment.Assignments)
                .Include(shipment => shipment.StatusHistory)
                .OrderByDescending(shipment => shipment.CreatedAtUtc),
            normalizedPageNumber,
            normalizedPageSize,
            cancellationToken);
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
        if (pageSize == int.MaxValue)
        {
            return int.MaxValue;
        }

        return Math.Clamp(pageSize, 1, 100);
    }

    private static IQueryable<Shipment> ApplyShipmentTextFilters(
        IQueryable<Shipment> query,
        string? searchText,
        bool includeReceiverPhone)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return query;
        }

        var keyword = searchText.Trim();
        var trackingCode = new TrackingCode(keyword);

        return includeReceiverPhone
            ? query.Where(shipment =>
                shipment.TrackingCode == trackingCode
                || shipment.ReceiverName.Contains(keyword)
                || shipment.ReceiverPhone.Value.Contains(keyword))
            : query.Where(shipment =>
                shipment.TrackingCode == trackingCode
                || shipment.ReceiverName.Contains(keyword));
    }

    private static IQueryable<Shipment> ApplyProvinceFilter(
        IQueryable<Shipment> query,
        string? province,
        bool pickupOnly)
    {
        if (string.IsNullOrWhiteSpace(province))
        {
            return query;
        }

        var normalizedProvince = province.Trim();

        return pickupOnly
            ? query.Where(shipment => shipment.PickupAddress.Province == normalizedProvince)
            : query.Where(shipment =>
                shipment.PickupAddress.Province == normalizedProvince
                || shipment.DeliveryAddress.Province == normalizedProvince);
    }

    private static IQueryable<Shipment> ApplyCreatedAtRange(
        IQueryable<Shipment> query,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc)
    {
        if (fromUtc.HasValue)
        {
            query = query.Where(shipment => shipment.CreatedAtUtc >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(shipment => shipment.CreatedAtUtc <= toUtc.Value);
        }

        return query;
    }

    private static IQueryable<Shipment> ApplyCodAmountRange(
        IQueryable<Shipment> query,
        decimal? minCodAmount,
        decimal? maxCodAmount)
    {
        if (minCodAmount.HasValue)
        {
            query = query.Where(shipment => shipment.CodAmount.Amount >= minCodAmount.Value);
        }

        if (maxCodAmount.HasValue)
        {
            query = query.Where(shipment => shipment.CodAmount.Amount <= maxCodAmount.Value);
        }

        return query;
    }

    private static async Task<PagedResult<Shipment>> ToPagedResultAsync(
        IQueryable<Shipment> query,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var totalCount = await query.CountAsync(cancellationToken);
        var pagedQuery = pageSize == int.MaxValue
            ? query
            : query.Skip((pageNumber - 1) * pageSize).Take(pageSize);
        var items = await pagedQuery.ToListAsync(cancellationToken);

        return new PagedResult<Shipment>(items, pageNumber, pageSize, totalCount);
    }
}
