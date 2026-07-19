using MiniLogistics.Domain.Common;

namespace MiniLogistics.Domain.AdminAuditing;

/// <summary>
/// Represents the Admin Audit Log domain entity.
/// </summary>
public sealed class AdminAuditLog : AuditableEntity
{
    private AdminAuditLog()
    {
        ActorRole = string.Empty;
        Action = string.Empty;
        TargetType = string.Empty;
    }

    public AdminAuditLog(
        Guid actorUserId,
        string actorRole,
        string action,
        string targetType,
        Guid targetId,
        DateTimeOffset createdAtUtc,
        string? oldValueJson = null,
        string? newValueJson = null,
        string? reason = null,
        string? ipAddress = null,
        string? userAgent = null)
        : base(Guid.NewGuid(), createdAtUtc)
    {
        if (actorUserId == Guid.Empty)
        {
            throw new DomainException("Actor user id is required.");
        }

        if (targetId == Guid.Empty)
        {
            throw new DomainException("Target id is required.");
        }

        ActorUserId = actorUserId;
        ActorRole = DomainGuard.RequireText(actorRole, nameof(actorRole), 50);
        Action = DomainGuard.RequireText(action, nameof(action), 120);
        TargetType = DomainGuard.RequireText(targetType, nameof(targetType), 80);
        TargetId = targetId;
        OldValueJson = DomainGuard.TrimOptional(oldValueJson, 4000);
        NewValueJson = DomainGuard.TrimOptional(newValueJson, 4000);
        Reason = DomainGuard.TrimOptional(reason, 500);
        IpAddress = DomainGuard.TrimOptional(ipAddress, 64);
        UserAgent = DomainGuard.TrimOptional(userAgent, 300);
    }

    public Guid ActorUserId { get; private set; }

    public string ActorRole { get; private set; }

    public string Action { get; private set; }

    public string TargetType { get; private set; }

    public Guid TargetId { get; private set; }

    public string? OldValueJson { get; private set; }

    public string? NewValueJson { get; private set; }

    public string? Reason { get; private set; }

    public string? IpAddress { get; private set; }

    public string? UserAgent { get; private set; }

}
