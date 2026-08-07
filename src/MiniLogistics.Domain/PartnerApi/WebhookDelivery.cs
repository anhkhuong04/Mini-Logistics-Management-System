using MiniLogistics.Domain.Common;

namespace MiniLogistics.Domain.PartnerApi;

/// <summary>
/// Represents the Webhook Delivery domain entity.
/// </summary>
public sealed class WebhookDelivery : AuditableEntity
{
    private WebhookDelivery()
    {
        EventType = string.Empty;
        PayloadJson = string.Empty;
        RowVersion = [];
    }

    public WebhookDelivery(
        Guid id,
        Guid webhookEndpointId,
        Guid apiClientId,
        string eventType,
        Guid aggregateId,
        string payloadJson,
        DateTimeOffset createdAtUtc,
        DateTimeOffset? nextAttemptAtUtc = null,
        string? protectedSigningSecret = null,
        int secretVersion = 1)
        : base(id, createdAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new DomainException("Webhook delivery id is required.");
        }

        if (webhookEndpointId == Guid.Empty)
        {
            throw new DomainException("Webhook endpoint id is required.");
        }

        if (apiClientId == Guid.Empty)
        {
            throw new DomainException("API client id is required.");
        }

        if (aggregateId == Guid.Empty)
        {
            throw new DomainException("Aggregate id is required.");
        }

        WebhookEndpointId = webhookEndpointId;
        ApiClientId = apiClientId;
        EventType = DomainGuard.RequireText(eventType, nameof(eventType), 100);
        AggregateId = aggregateId;
        PayloadJson = DomainGuard.RequireText(payloadJson, nameof(payloadJson), 4000);
        ProtectedSigningSecret = DomainGuard.TrimOptional(protectedSigningSecret, 2048);
        SecretVersion = secretVersion > 0
            ? secretVersion
            : throw new DomainException("Webhook secret version must be greater than zero.");
        Status = WebhookDeliveryStatus.Pending;
        NextAttemptAtUtc = nextAttemptAtUtc ?? createdAtUtc;
    }

    public Guid WebhookEndpointId { get; private set; }

    public Guid ApiClientId { get; private set; }

    public string EventType { get; private set; }

    public Guid AggregateId { get; private set; }

    public string PayloadJson { get; private set; }

    public string? ProtectedSigningSecret { get; private set; }

    public int SecretVersion { get; private set; }

    public WebhookDeliveryStatus Status { get; private set; }

    public int RetryCount { get; private set; }

    public DateTimeOffset? NextAttemptAtUtc { get; private set; }

    public DateTimeOffset? LastAttemptAtUtc { get; private set; }

    public int? LastResponseStatusCode { get; private set; }

    public long? LastDurationMs { get; private set; }

    public string? LastError { get; private set; }

    public string? LockedBy { get; private set; }

    public DateTimeOffset? LockedUntilUtc { get; private set; }

    public Guid? AttemptId { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public bool TryAcquireLease(
        string workerId,
        Guid attemptId,
        DateTimeOffset nowUtc,
        DateTimeOffset lockedUntilUtc)
    {
        if (Status is WebhookDeliveryStatus.Succeeded or WebhookDeliveryStatus.DeadLettered
            || NextAttemptAtUtc is null
            || NextAttemptAtUtc > nowUtc
            || (LockedUntilUtc.HasValue && LockedUntilUtc > nowUtc))
        {
            return false;
        }

        LockedBy = DomainGuard.RequireText(workerId, nameof(workerId), 200);
        AttemptId = attemptId == Guid.Empty
            ? throw new DomainException("Attempt id is required.")
            : attemptId;
        LockedUntilUtc = lockedUntilUtc > nowUtc
            ? lockedUntilUtc
            : throw new DomainException("Lease expiration must be in the future.");
        MarkUpdated(nowUtc);
        return true;
    }

    public void MarkSucceeded(
        int statusCode,
        DateTimeOffset attemptedAtUtc,
        long? durationMs = null)
    {
        Status = WebhookDeliveryStatus.Succeeded;
        LastAttemptAtUtc = attemptedAtUtc;
        LastResponseStatusCode = statusCode;
        LastDurationMs = NormalizeDuration(durationMs);
        LastError = null;
        NextAttemptAtUtc = null;
        ReleaseLease();
        MarkUpdated(attemptedAtUtc);
    }

    public void MarkFailed(
        int? statusCode,
        string error,
        DateTimeOffset attemptedAtUtc,
        DateTimeOffset? nextAttemptAtUtc,
        long? durationMs = null)
    {
        Status = nextAttemptAtUtc.HasValue
            ? WebhookDeliveryStatus.Failed
            : WebhookDeliveryStatus.DeadLettered;
        RetryCount++;
        LastAttemptAtUtc = attemptedAtUtc;
        LastResponseStatusCode = statusCode;
        LastDurationMs = NormalizeDuration(durationMs);
        LastError = DomainGuard.TrimOptional(error, 1000);
        NextAttemptAtUtc = nextAttemptAtUtc;
        ReleaseLease();
        MarkUpdated(attemptedAtUtc);
    }

    public Result Retry(DateTimeOffset queuedAtUtc)
    {
        if (Status is not (WebhookDeliveryStatus.Failed or WebhookDeliveryStatus.DeadLettered))
        {
            return Result.Failure(new Error(
                "WebhookDelivery.NotFailed",
                "Only failed or dead-lettered webhook deliveries can be retried."));
        }

        Status = WebhookDeliveryStatus.Pending;
        NextAttemptAtUtc = queuedAtUtc;
        LastError = null;
        ReleaseLease();
        MarkUpdated(queuedAtUtc);
        return Result.Success();
    }

    private void ReleaseLease()
    {
        LockedBy = null;
        LockedUntilUtc = null;
        AttemptId = null;
    }

    private static long? NormalizeDuration(long? durationMs)
    {
        return durationMs.HasValue && durationMs.Value >= 0
            ? durationMs.Value
            : null;
    }
}
