using Microsoft.EntityFrameworkCore;
using MiniLogistics.Application.Shops.Reports;
using MiniLogistics.Domain.CashOnDelivery;
using MiniLogistics.Domain.Shipments;
using MiniLogistics.Domain.ValueObjects;

namespace MiniLogistics.Infrastructure.Persistence.Repositories;

public sealed class ShopReportingRepository : IShopReportingRepository
{
    private readonly MiniLogisticsDbContext _dbContext;

    public ShopReportingRepository(MiniLogisticsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ShopDashboardKpiMetrics> GetDashboardKpiAsync(
        Guid shopId,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        CancellationToken cancellationToken = default)
    {
        var shipmentQuery = ApplyShipmentScope(
            _dbContext.Shipments.AsNoTracking(),
            shopId,
            fromUtc,
            toUtc);

        var countByStatus = await shipmentQuery
            .GroupBy(shipment => shipment.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToDictionaryAsync(row => row.Status, row => row.Count, cancellationToken);

        var shippingFees = await shipmentQuery
            .Select(shipment => shipment.ShippingFee)
            .ToListAsync(cancellationToken);
        var totalShippingFee = shippingFees.Sum(fee => fee.Amount);

        var codRows = await CreateCodRowsQuery(shopId, fromUtc, toUtc)
            .ToListAsync(cancellationToken);
        var pendingCodAmount = codRows
            .Where(row => row.CodStatus == CodStatus.PendingCollection)
            .Sum(row => row.DeclaredAmount.Amount);
        var collectedCodAmount = codRows
            .Where(row => row.CodStatus == CodStatus.Collected || row.CodStatus == CodStatus.Settled)
            .Sum(row => (row.CollectedAmount ?? row.DeclaredAmount).Amount);
        var settledCodAmount = codRows
            .Where(row => row.CodStatus == CodStatus.Settled)
            .Sum(row => (row.CollectedAmount ?? row.DeclaredAmount).Amount);

        return new ShopDashboardKpiMetrics(
            countByStatus.Values.Sum(),
            countByStatus.GetValueOrDefault(ShipmentStatus.Delivered),
            countByStatus.GetValueOrDefault(ShipmentStatus.Returned),
            countByStatus.GetValueOrDefault(ShipmentStatus.DeliveryFailed),
            totalShippingFee,
            pendingCodAmount,
            collectedCodAmount,
            settledCodAmount,
            "VND",
            countByStatus);
    }

    public async Task<IReadOnlyList<ShopCodReportRowResponse>> GetCodReportRowsAsync(
        Guid shopId,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        int maxRows,
        CancellationToken cancellationToken = default)
    {
        var rows = await CreateCodRowsQuery(shopId, fromUtc, toUtc)
            .OrderByDescending(row => row.CreatedAtUtc)
            .Take(Math.Clamp(maxRows, 1, 10_000))
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new ShopCodReportRowResponse(
                row.ShipmentId,
                row.TrackingCode.Value,
                row.ShipmentStatus,
                row.CodStatus,
                row.DeclaredAmount.Amount,
                row.CollectedAmount?.Amount,
                row.DiscrepancyAmount?.Amount ?? 0m,
                row.CollectedAtUtc,
                row.CreatedAtUtc))
            .ToList();
    }

    private static IQueryable<Shipment> ApplyShipmentScope(
        IQueryable<Shipment> query,
        Guid shopId,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc)
    {
        query = query.Where(shipment => shipment.ShopId == shopId);

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

    private IQueryable<ShopCodReportRowProjection> CreateCodRowsQuery(
        Guid shopId,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc)
    {
        var shipmentQuery = ApplyShipmentScope(
            _dbContext.Shipments.AsNoTracking(),
            shopId,
            fromUtc,
            toUtc);

        return
            from shipment in shipmentQuery
            join cod in _dbContext.CodTransactions.AsNoTracking()
                on shipment.Id equals cod.ShipmentId
            select new ShopCodReportRowProjection(
                shipment.Id,
                shipment.TrackingCode,
                shipment.Status,
                cod.Status,
                cod.Amount,
                cod.CollectedAmount,
                cod.DiscrepancyAmount,
                cod.CollectedAtUtc,
                shipment.CreatedAtUtc);
    }

    private sealed record ShopCodReportRowProjection(
        Guid ShipmentId,
        TrackingCode TrackingCode,
        ShipmentStatus ShipmentStatus,
        CodStatus CodStatus,
        Money DeclaredAmount,
        Money? CollectedAmount,
        Money? DiscrepancyAmount,
        DateTimeOffset? CollectedAtUtc,
        DateTimeOffset CreatedAtUtc);
}
