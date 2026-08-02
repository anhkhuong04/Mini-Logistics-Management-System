namespace MiniLogistics.Application.Shops.Audit;

public sealed record ShopAuditLogQuery(
    Guid CurrentUserId,
    Guid? ShopId = null,
    string? Action = null,
    DateTimeOffset? FromUtc = null,
    DateTimeOffset? ToUtc = null,
    int Limit = 200);
