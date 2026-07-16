namespace MiniLogistics.Application.AdminAuditing;

public sealed record AdminAuditLogQuery(
    Guid RequestedByUserId,
    Guid? ActorUserId = null,
    string? Action = null,
    string? TargetType = null,
    DateTimeOffset? FromUtc = null,
    DateTimeOffset? ToUtc = null,
    int Limit = 200);
