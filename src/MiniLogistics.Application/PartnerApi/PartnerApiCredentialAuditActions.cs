namespace MiniLogistics.Application.PartnerApi;

public static class PartnerApiCredentialAuditActions
{
    public const string ApiClientCreated = nameof(ApiClientCreated);
    public const string ApiClientKeyRotated = nameof(ApiClientKeyRotated);
    public const string ApiClientActivated = nameof(ApiClientActivated);
    public const string ApiClientDeactivated = nameof(ApiClientDeactivated);
    public const string WebhookEndpointUpserted = nameof(WebhookEndpointUpserted);
    public const string WebhookTestQueued = nameof(WebhookTestQueued);
}
