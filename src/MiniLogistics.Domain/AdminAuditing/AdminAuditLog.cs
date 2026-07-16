using MiniLogistics.Domain.Common;

namespace MiniLogistics.Domain.AdminAuditing;

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
        string? oldValueJson = null,
        string? newValueJson = null,
        string? reason = null,
        string? ipAddress = null,
        string? userAgent = null)
        : base(Guid.NewGuid())
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
        ActorRole = RequireText(actorRole, nameof(actorRole), 50);
        Action = RequireText(action, nameof(action), 120);
        TargetType = RequireText(targetType, nameof(targetType), 80);
        TargetId = targetId;
        OldValueJson = TrimOptional(oldValueJson, 4000);
        NewValueJson = TrimOptional(newValueJson, 4000);
        Reason = TrimOptional(reason, 500);
        IpAddress = TrimOptional(ipAddress, 64);
        UserAgent = TrimOptional(userAgent, 300);
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

    private static string RequireText(string value, string fieldName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException($"{fieldName} is required.");
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw new DomainException($"{fieldName} cannot exceed {maxLength} characters.");
        }

        return trimmed;
    }

    private static string? TrimOptional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed[..Math.Min(trimmed.Length, maxLength)];
    }
}
