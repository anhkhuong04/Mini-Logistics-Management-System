using MiniLogistics.Domain.CashOnDelivery;
using MiniLogistics.Domain.Shipments;

namespace MiniLogistics.Application.Shops.Reports;

public sealed record ShopCodReportResponse(
    Guid ShopId,
    decimal PendingCollectionAmount,
    decimal CollectedAmount,
    decimal SettledAmount,
    decimal DiscrepancyAmount,
    string Currency,
    IReadOnlyList<ShopCodReportRowResponse> Rows);

public sealed record ShopCodReportRowResponse(
    Guid ShipmentId,
    string TrackingCode,
    ShipmentStatus ShipmentStatus,
    CodStatus CodStatus,
    decimal DeclaredAmount,
    decimal? CollectedAmount,
    decimal DiscrepancyAmount,
    DateTimeOffset? CollectedAtUtc,
    DateTimeOffset CreatedAtUtc);
