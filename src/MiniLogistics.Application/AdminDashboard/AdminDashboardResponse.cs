namespace MiniLogistics.Application.AdminDashboard;

public sealed record AdminDashboardResponse(
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc,
    string? Province,
    ShipmentOverviewMetrics Shipments,
    ShipperOverviewMetrics Shippers,
    CodOverviewMetrics Cod,
    ShopOverviewMetrics Shops,
    WebhookOverviewMetrics Webhooks);

public sealed record ShipmentOverviewMetrics(
    int Created,
    int PendingPickupUnassigned,
    int DeliveryFailed,
    int NonDraftTotal,
    decimal DeliveryFailedRate);

public sealed record ShipperOverviewMetrics(
    int Active,
    int AvailableForAssignment,
    int OverCapacity);

public sealed record CodOverviewMetrics(
    int PendingCollectionCount,
    decimal PendingCollectionAmount,
    int CollectedAwaitingSettlementCount,
    decimal CollectedAwaitingSettlementAmount,
    int SettledCount,
    decimal SettledAmount,
    string Currency);

public sealed record ShopOverviewMetrics(
    int Active,
    int Inactive);

public sealed record WebhookOverviewMetrics(
    int Failed,
    int RetryPending);
