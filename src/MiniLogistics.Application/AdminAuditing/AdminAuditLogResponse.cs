namespace MiniLogistics.Application.AdminAuditing;

public sealed record AdminAuditLogResponse(
    Guid AuditLogId,
    Guid ActorUserId,
    string ActorRole,
    string Action,
    string TargetType,
    Guid TargetId,
    string? OldValueJson,
    string? NewValueJson,
    string? Reason,
    string? IpAddress,
    string? UserAgent,
    DateTimeOffset CreatedAtUtc);
