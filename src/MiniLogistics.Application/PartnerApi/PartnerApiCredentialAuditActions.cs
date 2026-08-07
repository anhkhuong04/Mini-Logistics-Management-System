namespace MiniLogistics.Application.PartnerApi;

public static class PartnerApiCredentialAuditActions
{
    public const string ApiClientCreated = nameof(ApiClientCreated);
    public const string ApiClientKeyRotated = nameof(ApiClientKeyRotated);
    public const string ApiClientActivated = nameof(ApiClientActivated);
    public const string ApiClientDeactivated = nameof(ApiClientDeactivated);
    public const string ApiClientSecurityUpdated = nameof(ApiClientSecurityUpdated);
    public const string WebhookEndpointUpserted = nameof(WebhookEndpointUpserted);
    public const string WebhookTestQueued = nameof(WebhookTestQueued);
    public const string WebhookDeliveryRetried = nameof(WebhookDeliveryRetried);
    public const string OutboxMessageRetried = nameof(OutboxMessageRetried);
}
