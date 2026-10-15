using Microsoft.EntityFrameworkCore;
using MiniLogistics.Application.AdminDashboard;
using MiniLogistics.Application.Shipments;
using MiniLogistics.Domain.CashOnDelivery;
using MiniLogistics.Domain.PartnerApi;
using MiniLogistics.Domain.Shipments;
using MiniLogistics.Domain.ValueObjects;

namespace MiniLogistics.Infrastructure.Persistence.Repositories;

public sealed class AdminDashboardMetricsRepository : IAdminDashboardMetricsRepository
{
    private readonly MiniLogisticsDbContext _dbContext;

    public AdminDashboardMetricsRepository(MiniLogisticsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AdminDashboardRepositoryMetrics> GetAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        string? province,
        IReadOnlyCollection<ActiveShipperCapacity> activeShippers,
        CancellationToken cancellationToken = default)
    {
        var shipmentsInRange = ApplyShipmentFilters(
            _dbContext.Shipments.AsNoTracking().Where(shipment => shipment.Status != ShipmentStatus.Draft),
            fromUtc,
            toUtc,
            province);

        var nonDraftTotal = await shipmentsInRange.CountAsync(cancellationToken);
        var created = nonDraftTotal;
        var deliveryFailed = await shipmentsInRange
            .CountAsync(shipment => shipment.Status == ShipmentStatus.DeliveryFailed, cancellationToken);
        var pendingPickupUnassigned = await shipmentsInRange
            .Where(shipment => shipment.Status == ShipmentStatus.PendingPickup)
            .CountAsync(shipment => !shipment.Assignments.Any(assignment => assignment.UnassignedAtUtc == null), cancellationToken);

        var activeShipperIds = activeShippers.Select(shipper => shipper.ShipperId).ToList();
        var activeLoads = activeShipperIds.Count == 0
            ? new Dictionary<Guid, int>()
            : await _dbContext.ShipmentAssignments
                .AsNoTracking()
                .Where(assignment => activeShipperIds.Contains(assignment.ShipperId))
                .Where(assignment => assignment.UnassignedAtUtc == null)
                .Join(
                    _dbContext.Shipments.AsNoTracking(),
                    assignment => assignment.ShipmentId,
                    shipment => shipment.Id,
                    (assignment, shipment) => new { assignment, shipment })
                .Where(row => ShipmentLoadStatuses.ActiveAssignmentStatuses.Contains(row.shipment.Status))
                .GroupBy(row => row.assignment.ShipperId)
                .Select(group => new { ShipperId = group.Key, Count = group.Count() })
                .ToDictionaryAsync(row => row.ShipperId, row => row.Count, cancellationToken);

        var overCapacity = activeShippers.Count(shipper =>
            activeLoads.GetValueOrDefault(shipper.ShipperId) > shipper.MaxActiveShipments);

        var codRows = await CreateCodRowsQuery(fromUtc, toUtc, province)
            .ToListAsync(cancellationToken);
        var pendingCollectionRows = codRows
            .Where(row => row.ShipmentStatus == ShipmentStatus.Delivered && row.CodStatus == CodStatus.PendingCollection)
            .ToList();
        var collectedRows = codRows.Where(row => row.CodStatus == CodStatus.Collected).ToList();
        var settledRows = codRows.Where(row => row.CodStatus == CodStatus.Settled).ToList();

        var shopQuery = _dbContext.Shops.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(province))
        {
            shopQuery = shopQuery.Where(shop => shop.Address.Province == province);
        }

        var activeShops = await shopQuery.CountAsync(shop => shop.IsActive, cancellationToken);
        var inactiveShops = await shopQuery.CountAsync(shop => !shop.IsActive, cancellationToken);

        var webhookQuery = _dbContext.WebhookDeliveries
            .AsNoTracking()
            .Where(delivery => delivery.CreatedAtUtc >= fromUtc && delivery.CreatedAtUtc <= toUtc);
        var failedWebhooks = await webhookQuery.CountAsync(
            delivery => delivery.Status == WebhookDeliveryStatus.Failed
                || delivery.Status == WebhookDeliveryStatus.DeadLettered,
            cancellationToken);
        var retryPendingWebhooks = await webhookQuery.CountAsync(
            delivery => delivery.NextAttemptAtUtc != null
                && delivery.Status != WebhookDeliveryStatus.Succeeded,
            cancellationToken);

        return new AdminDashboardRepositoryMetrics(
            new ShipmentOverviewMetrics(
                created,
                pendingPickupUnassigned,
                deliveryFailed,
                nonDraftTotal,
                nonDraftTotal == 0 ? 0m : Math.Round((decimal)deliveryFailed / nonDraftTotal * 100, 2)),
            new ShipperOverviewMetrics(
                activeShippers.Count,
                activeShippers.Count(shipper => shipper.IsAvailableForAssignment),
                overCapacity),
            new CodOverviewMetrics(
                pendingCollectionRows.Count,
                pendingCollectionRows.Sum(row => row.Amount.Amount),
                collectedRows.Count,
                collectedRows.Sum(row => row.Amount.Amount),
                settledRows.Count,
                settledRows.Sum(row => row.Amount.Amount),
                "VND"),
            new ShopOverviewMetrics(activeShops, inactiveShops),
            new WebhookOverviewMetrics(failedWebhooks, retryPendingWebhooks));
    }

    private static IQueryable<Shipment> ApplyShipmentFilters(
        IQueryable<Shipment> query,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        string? province)
    {
        query = query.Where(shipment => shipment.CreatedAtUtc >= fromUtc && shipment.CreatedAtUtc <= toUtc);
        if (!string.IsNullOrWhiteSpace(province))
        {
            query = query.Where(shipment => shipment.PickupAddress.Province == province);
        }

        return query;
    }

    private IQueryable<CodDashboardRow> CreateCodRowsQuery(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        string? province)
    {
        var query = _dbContext.CodTransactions
            .AsNoTracking()
            .Join(
                _dbContext.Shipments.AsNoTracking(),
                cod => cod.ShipmentId,
                shipment => shipment.Id,
                (cod, shipment) => new { Cod = cod, Shipment = shipment })
            .Where(row => row.Shipment.Status != ShipmentStatus.Draft)
            .Where(row => row.Shipment.CreatedAtUtc >= fromUtc && row.Shipment.CreatedAtUtc <= toUtc);

        if (!string.IsNullOrWhiteSpace(province))
        {
            query = query.Where(row => row.Shipment.PickupAddress.Province == province);
        }

        return query.Select(row => new CodDashboardRow(
            row.Shipment.CreatedAtUtc,
            row.Shipment.PickupAddress.Province,
            row.Shipment.Status,
            row.Cod.Status,
            row.Cod.Amount));
    }

    private sealed record CodDashboardRow(
        DateTimeOffset ShipmentCreatedAtUtc,
        string Province,
        ShipmentStatus ShipmentStatus,
        CodStatus CodStatus,
        Money Amount);
}
