namespace MiniLogistics.Application.CashOnDelivery.GetShipperCodDailySummary;

public sealed record ShipperCodDailySummaryResponse(
    Guid ShipperUserId,
    DateOnly BusinessDate,
    decimal PendingCollectionAmount,
    int PendingCollectionCount,
    decimal CollectedTodayAmount,
    int CollectedTodayCount,
    string Currency);
