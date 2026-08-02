using Microsoft.EntityFrameworkCore;
using MiniLogistics.Application.Shops.Reports;
using MiniLogistics.Domain.CashOnDelivery;
using MiniLogistics.Domain.Shipments;

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

        var totalShippingFee = await shipmentQuery
            .SumAsync(shipment => (decimal?)shipment.ShippingFee.Amount, cancellationToken) ?? 0m;

        var codQuery = CreateCodRowsQuery(shopId, fromUtc, toUtc);
        var pendingCodAmount = await codQuery
            .Where(row => row.CodStatus == CodStatus.PendingCollection)
            .SumAsync(row => (decimal?)row.DeclaredAmount, cancellationToken) ?? 0m;
        var collectedCodAmount = await codQuery
            .Where(row => row.CodStatus == CodStatus.Collected || row.CodStatus == CodStatus.Settled)
            .SumAsync(row => (decimal?)(row.CollectedAmount ?? row.DeclaredAmount), cancellationToken) ?? 0m;
        var settledCodAmount = await codQuery
            .Where(row => row.CodStatus == CodStatus.Settled)
            .SumAsync(row => (decimal?)(row.CollectedAmount ?? row.DeclaredAmount), cancellationToken) ?? 0m;

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
                row.TrackingCode,
                row.ShipmentStatus,
                row.CodStatus,
                row.DeclaredAmount,
                row.CollectedAmount,
                row.DiscrepancyAmount ?? 0m,
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
                shipment.TrackingCode.Value,
                shipment.Status,
                cod.Status,
                cod.Amount.Amount,
                cod.CollectedAmount == null ? null : cod.CollectedAmount.Amount,
                cod.DiscrepancyAmount == null ? null : cod.DiscrepancyAmount.Amount,
                cod.CollectedAtUtc,
                shipment.CreatedAtUtc);
    }

    private sealed record ShopCodReportRowProjection(
        Guid ShipmentId,
        string TrackingCode,
        ShipmentStatus ShipmentStatus,
        CodStatus CodStatus,
        decimal DeclaredAmount,
        decimal? CollectedAmount,
        decimal? DiscrepancyAmount,
        DateTimeOffset? CollectedAtUtc,
        DateTimeOffset CreatedAtUtc);
}

