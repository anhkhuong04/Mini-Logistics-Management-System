using MiniLogistics.Domain.Common;

namespace MiniLogistics.Domain.PartnerApi;

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
        string? traceId = null,
        string? ipHash = null,
        string? userAgent = null,
        string? errorCode = null,
        string? errorMessage = null)
        : base(Guid.NewGuid())
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
        Action = RequireText(action, nameof(action), 100);
        IsSuccess = isSuccess;
        TraceId = TrimOptional(traceId, 100);
        IpHash = TrimOptional(ipHash, 128);
        UserAgent = TrimOptional(userAgent, 300);
        ErrorCode = TrimOptional(errorCode, 100);
        ErrorMessage = TrimOptional(errorMessage, 500);
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
