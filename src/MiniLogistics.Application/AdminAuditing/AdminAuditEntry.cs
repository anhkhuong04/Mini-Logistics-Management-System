namespace MiniLogistics.Application.AdminAuditing;

public sealed record AdminAuditEntry(
    Guid ActorUserId,
    string Action,
    string TargetType,
    Guid TargetId,
    object? OldValue = null,
    object? NewValue = null,
    string? Reason = null,
    string? ActorRole = null);
