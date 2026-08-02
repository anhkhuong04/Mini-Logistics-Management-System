using MiniLogistics.Domain.CashOnDelivery;
using MiniLogistics.Domain.Shipments;

namespace MiniLogistics.Application.Shops.Reports;

public interface IShopReportingRepository
{
    Task<ShopDashboardKpiMetrics> GetDashboardKpiAsync(
        Guid shopId,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ShopCodReportRowResponse>> GetCodReportRowsAsync(
        Guid shopId,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        int maxRows,
        CancellationToken cancellationToken = default);
}

public sealed record ShopDashboardKpiMetrics(
    int TotalShipments,
    int DeliveredShipments,
    int ReturnedShipments,
    int DeliveryFailedShipments,
    decimal TotalShippingFee,
    decimal PendingCodAmount,
    decimal CollectedCodAmount,
    decimal SettledCodAmount,
    string Currency,
    IReadOnlyDictionary<ShipmentStatus, int> CountByStatus);

