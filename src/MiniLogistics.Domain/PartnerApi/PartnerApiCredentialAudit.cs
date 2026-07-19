using MiniLogistics.Domain.Common;

namespace MiniLogistics.Domain.PartnerApi;

/// <summary>
/// Represents the Partner Api Credential Audit domain entity.
/// </summary>
public sealed class PartnerApiCredentialAudit : AuditableEntity
{
    private PartnerApiCredentialAudit()
    {
        Action = string.Empty;
    }

    public PartnerApiCredentialAudit(
        Guid actorUserId,
        Guid shopId,
        Guid? apiClientId,
        string action,
        bool isSuccess,
        DateTimeOffset createdAtUtc,
        string? traceId = null,
        string? ipHash = null,
        string? userAgent = null,
        string? errorCode = null,
        string? errorMessage = null)
        : base(Guid.NewGuid(), createdAtUtc)
    {
        if (actorUserId == Guid.Empty)
        {
            throw new DomainException("Actor user id is required.");
        }

        if (shopId == Guid.Empty)
        {
            throw new DomainException("Shop id is required.");
        }

        ActorUserId = actorUserId;
        ShopId = shopId;
        ApiClientId = apiClientId;
        Action = DomainGuard.RequireText(action, nameof(action), 100);
        IsSuccess = isSuccess;
        TraceId = DomainGuard.TrimOptional(traceId, 100);
        IpHash = DomainGuard.TrimOptional(ipHash, 128);
        UserAgent = DomainGuard.TrimOptional(userAgent, 300);
        ErrorCode = DomainGuard.TrimOptional(errorCode, 100);
        ErrorMessage = DomainGuard.TrimOptional(errorMessage, 500);
    }

    public Guid ActorUserId { get; private set; }

    public Guid ShopId { get; private set; }

    public Guid? ApiClientId { get; private set; }

    public string Action { get; private set; }

    public bool IsSuccess { get; private set; }

    public string? TraceId { get; private set; }

    public string? IpHash { get; private set; }

    public string? UserAgent { get; private set; }

    public string? ErrorCode { get; private set; }

    public string? ErrorMessage { get; private set; }

}
