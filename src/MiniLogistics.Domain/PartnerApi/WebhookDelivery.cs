using MiniLogistics.Domain.Common;

namespace MiniLogistics.Domain.PartnerApi;

public sealed class WebhookDelivery : AuditableEntity
{
    private WebhookDelivery()
    {
        EventType = string.Empty;
        PayloadJson = string.Empty;
    }

    public WebhookDelivery(
        Guid id,
        Guid webhookEndpointId,
        Guid apiClientId,
        string eventType,
        Guid aggregateId,
        string payloadJson,
        DateTimeOffset? nextAttemptAtUtc = null)
        : base(id)
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
        EventType = RequireText(eventType, nameof(eventType), 100);
        AggregateId = aggregateId;
        PayloadJson = RequireText(payloadJson, nameof(payloadJson), 4000);
        Status = WebhookDeliveryStatus.Pending;
        NextAttemptAtUtc = nextAttemptAtUtc ?? DateTimeOffset.UtcNow;
    }

    public Guid WebhookEndpointId { get; private set; }

    public Guid ApiClientId { get; private set; }

    public string EventType { get; private set; }

    public Guid AggregateId { get; private set; }

    public string PayloadJson { get; private set; }

    public WebhookDeliveryStatus Status { get; private set; }

    public int RetryCount { get; private set; }

    public DateTimeOffset? NextAttemptAtUtc { get; private set; }

    public DateTimeOffset? LastAttemptAtUtc { get; private set; }

    public int? LastResponseStatusCode { get; private set; }

    public long? LastDurationMs { get; private set; }

    public string? LastError { get; private set; }

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
        MarkUpdated();
    }

    public void MarkFailed(
        int? statusCode,
        string error,
        DateTimeOffset attemptedAtUtc,
        DateTimeOffset? nextAttemptAtUtc,
        long? durationMs = null)
    {
        Status = WebhookDeliveryStatus.Failed;
        RetryCount++;
        LastAttemptAtUtc = attemptedAtUtc;
        LastResponseStatusCode = statusCode;
        LastDurationMs = NormalizeDuration(durationMs);
        LastError = string.IsNullOrWhiteSpace(error) ? null : error.Trim()[..Math.Min(error.Trim().Length, 1000)];
        NextAttemptAtUtc = nextAttemptAtUtc;
        MarkUpdated();
    }

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

    private static long? NormalizeDuration(long? durationMs)
    {
        return durationMs.HasValue && durationMs.Value >= 0
            ? durationMs.Value
            : null;
    }
}
