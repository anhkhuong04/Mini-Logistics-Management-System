using MiniLogistics.Domain.PartnerApi;

namespace MiniLogistics.Application.PartnerApi;

public static class PartnerIntegrationDashboardMapper
{
    public static PartnerWebhookEndpointResponse MapEndpoint(WebhookEndpoint endpoint)
    {
        return new PartnerWebhookEndpointResponse(
            endpoint.Id,
            endpoint.Url,
            endpoint.IsActive,
            endpoint.CreatedAtUtc,
            endpoint.UpdatedAtUtc);
    }

    public static PartnerWebhookDeliveryResponse MapDelivery(WebhookDelivery delivery)
    {
        return new PartnerWebhookDeliveryResponse(
            delivery.Id,
            delivery.EventType,
            delivery.Status,
            delivery.RetryCount,
            delivery.NextAttemptAtUtc,
            delivery.LastAttemptAtUtc,
            delivery.LastResponseStatusCode,
            delivery.LastDurationMs,
            delivery.LastError,
            delivery.CreatedAtUtc);
    }

    public static PartnerWebhookMetricsResponse BuildWebhookMetrics(
        IReadOnlyList<PartnerWebhookDeliveryResponse> deliveries)
    {
        var total = deliveries.Count;
        var succeeded = deliveries.Count(delivery => delivery.Status == WebhookDeliveryStatus.Succeeded);
        var failed = deliveries.Count(delivery => delivery.Status == WebhookDeliveryStatus.Failed);
        var pendingRetry = deliveries.Count(delivery =>
            delivery.Status != WebhookDeliveryStatus.Succeeded
            && delivery.NextAttemptAtUtc.HasValue);
        var successRate = total == 0
            ? 0
            : decimal.Round((decimal)succeeded / total * 100, 2);
        var durations = deliveries
            .Where(delivery => delivery.LastDurationMs.HasValue)
            .Select(delivery => delivery.LastDurationMs!.Value)
            .ToList();
        var averageLatencyMs = durations.Count == 0
            ? (decimal?)null
            : decimal.Round((decimal)durations.Average(), 2);

        return new PartnerWebhookMetricsResponse(
            total,
            succeeded,
            failed,
            pendingRetry,
            successRate,
            averageLatencyMs);
    }

    public static PartnerApiCredentialAuditResponse MapCredentialAudit(PartnerApiCredentialAudit audit)
    {
        return new PartnerApiCredentialAuditResponse(
            audit.Id,
            audit.ActorUserId,
            audit.Action,
            audit.IsSuccess,
            audit.ErrorCode,
            audit.CreatedAtUtc);
    }
}
