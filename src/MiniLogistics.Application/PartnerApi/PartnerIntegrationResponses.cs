using MiniLogistics.Domain.PartnerApi;

namespace MiniLogistics.Application.PartnerApi;

public sealed record PartnerIntegrationDashboardResponse(
    IReadOnlyList<PartnerIntegrationShopResponse> Shops,
    IReadOnlyList<PartnerApiClientResponse> ApiClients,
    bool GranularPermissionEnabled = false);

public sealed record PartnerIntegrationShopResponse(
    Guid ShopId,
    string ShopName,
    bool IsActive);

public sealed record PartnerApiClientResponse(
    Guid ApiClientId,
    Guid ShopId,
    string ShopName,
    string Name,
    string ApiKeyPrefix,
    bool IsActive,
    DateTimeOffset? LastUsedAtUtc,
    DateTimeOffset CreatedAtUtc,
    PartnerWebhookEndpointResponse? WebhookEndpoint,
    IReadOnlyList<PartnerWebhookDeliveryResponse> RecentDeliveries,
    PartnerWebhookMetricsResponse WebhookMetrics,
    IReadOnlyList<PartnerApiCredentialAuditResponse> RecentCredentialAudits);

public sealed record PartnerWebhookMetricsResponse(
    int TotalDeliveries,
    int SucceededDeliveries,
    int FailedDeliveries,
    int PendingRetryDeliveries,
    decimal SuccessRate,
    decimal? AverageLatencyMs);

public sealed record PartnerWebhookEndpointResponse(
    Guid WebhookEndpointId,
    string Url,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc);

public sealed record PartnerWebhookDeliveryResponse(
    Guid WebhookDeliveryId,
    string EventType,
    WebhookDeliveryStatus Status,
    int RetryCount,
    DateTimeOffset? NextAttemptAtUtc,
    DateTimeOffset? LastAttemptAtUtc,
    int? LastResponseStatusCode,
    long? LastDurationMs,
    string? LastError,
    DateTimeOffset CreatedAtUtc);

public sealed record PartnerApiClientSecretResponse(
    Guid ApiClientId,
    string ApiKey,
    string ApiKeyPrefix);

public sealed record PartnerWebhookTestResponse(
    Guid WebhookDeliveryId,
    string EventType);

public sealed record PartnerApiCredentialAuditResponse(
    Guid AuditId,
    Guid ActorUserId,
    string Action,
    bool IsSuccess,
    string? ErrorCode,
    DateTimeOffset OccurredAtUtc);
