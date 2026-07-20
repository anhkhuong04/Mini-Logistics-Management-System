using MiniLogistics.Domain.Shipments;

namespace MiniLogistics.Application.Shops.Reports;

public sealed record ShopDashboardKpiResponse(
    Guid ShopId,
    int TotalShipments,
    int DeliveredShipments,
    int ReturnedShipments,
    int DeliveryFailedShipments,
    decimal DeliveredRate,
    decimal ReturnedRate,
    decimal DeliveryFailedRate,
    decimal TotalShippingFee,
    decimal PendingCodAmount,
    decimal CollectedCodAmount,
    decimal SettledCodAmount,
    string Currency,
    IReadOnlyDictionary<ShipmentStatus, int> CountByStatus);
