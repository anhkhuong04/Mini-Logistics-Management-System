using MiniLogistics.Domain.CashOnDelivery;
using MiniLogistics.Domain.Shipments;

namespace MiniLogistics.Application.AdminCod;

public sealed record AdminCodReportResponse(
    AdminCodSummary Summary,
    IReadOnlyList<AdminCodTransactionResponse> Items,
    IReadOnlyList<AdminCodGroupSummary> ByShipper,
    IReadOnlyList<AdminCodGroupSummary> ByProvince,
    IReadOnlyList<AdminCodDailySummary> ByDay);

public sealed record AdminCodSummary(
    int PendingCollectionCount,
    decimal PendingCollectionAmount,
    int CollectedAwaitingSettlementCount,
    decimal CollectedAwaitingSettlementAmount,
    int SettledCount,
    decimal SettledAmount,
    string Currency);

public sealed record AdminCodTransactionResponse(
    Guid ShipmentId,
    string TrackingCode,
    string ReceiverName,
    string ReceiverPhone,
    string Province,
    Guid? ShipperId,
    string? ShipperName,
    decimal Amount,
    string Currency,
    CodStatus CodStatus,
    ShipmentStatus ShipmentStatus,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? CollectedAtUtc,
    Guid? CollectedByUserId,
    DateTimeOffset? SettledAtUtc,
    Guid? SettledByUserId);

public sealed record AdminCodGroupSummary(
    string GroupKey,
    int ShipmentCount,
    decimal PendingAmount,
    decimal CollectedAmount,
    decimal SettledAmount,
    decimal CollectedRate);

public sealed record AdminCodDailySummary(
    DateOnly Day,
    int ShipmentCount,
    decimal PendingAmount,
    decimal CollectedAmount,
    decimal SettledAmount);
