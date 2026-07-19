using MiniLogistics.Domain.Common;

namespace MiniLogistics.Domain.PartnerApi;

/// <summary>
/// Represents the Partner Api Request Audit domain entity.
/// </summary>
public sealed class PartnerApiRequestAudit : AuditableEntity
{
    private PartnerApiRequestAudit()
    {
        Method = string.Empty;
        Path = string.Empty;
        TraceId = string.Empty;
        RequestHash = string.Empty;
    }

    public PartnerApiRequestAudit(
        Guid apiClientId,
        Guid shopId,
        string method,
        string path,
        string traceId,
        string? externalOrderId,
        string? idempotencyKey,
        string requestHash,
        int statusCode,
        int durationMs,
        bool isSuccess,
        bool isIdempotentReplay,
        Guid? shipmentId,
        string? trackingCode,
        string? errorCode,
        string? errorMessage,
        DateTimeOffset createdAtUtc)
        : base(Guid.NewGuid(), createdAtUtc)
    {
        if (apiClientId == Guid.Empty)
        {
            throw new DomainException("API client id is required.");
        }

        if (shopId == Guid.Empty)
        {
            throw new DomainException("Shop id is required.");
        }

        ApiClientId = apiClientId;
        ShopId = shopId;
        Method = DomainGuard.RequireText(method, nameof(method), 20);
        Path = DomainGuard.RequireText(path, nameof(path), 300);
        TraceId = DomainGuard.RequireText(traceId, nameof(traceId), 100);
        ExternalOrderId = DomainGuard.TrimOptional(externalOrderId, 100);
        IdempotencyKey = DomainGuard.TrimOptional(idempotencyKey, 150);
        RequestHash = DomainGuard.RequireText(requestHash, nameof(requestHash), 128);
        StatusCode = statusCode;
        DurationMs = Math.Max(0, durationMs);
        IsSuccess = isSuccess;
        IsIdempotentReplay = isIdempotentReplay;
        ShipmentId = shipmentId;
        TrackingCode = DomainGuard.TrimOptional(trackingCode, 50);
        ErrorCode = DomainGuard.TrimOptional(errorCode, 100);
        ErrorMessage = DomainGuard.TrimOptional(errorMessage, 500);
    }

    public Guid ApiClientId { get; private set; }

    public Guid ShopId { get; private set; }

    public string Method { get; private set; }

    public string Path { get; private set; }

    public string TraceId { get; private set; }

    public string? ExternalOrderId { get; private set; }

    public string? IdempotencyKey { get; private set; }

    public string RequestHash { get; private set; }

    public int StatusCode { get; private set; }

    public int DurationMs { get; private set; }

    public bool IsSuccess { get; private set; }

    public bool IsIdempotentReplay { get; private set; }

    public Guid? ShipmentId { get; private set; }

    public string? TrackingCode { get; private set; }

    public string? ErrorCode { get; private set; }

    public string? ErrorMessage { get; private set; }

}
