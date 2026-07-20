namespace MiniLogistics.Application.Shops.Reports;

public sealed record ShopDashboardKpiQuery(
    Guid OwnerUserId,
    Guid? ShopId = null,
    DateTimeOffset? FromUtc = null,
    DateTimeOffset? ToUtc = null);
