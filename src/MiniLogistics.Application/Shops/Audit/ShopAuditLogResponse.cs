namespace MiniLogistics.Application.Shops.Audit;

public sealed record ShopAuditLogResponse(
    Guid AuditLogId,
    string ActorRole,
    string Action,
    string TargetType,
    Guid TargetId,
    string? OldValueJson,
    string? NewValueJson,
    string? Reason,
    DateTimeOffset CreatedAtUtc);
