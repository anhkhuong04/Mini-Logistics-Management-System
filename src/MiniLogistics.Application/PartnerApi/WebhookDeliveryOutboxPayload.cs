namespace MiniLogistics.Application.PartnerApi;

public sealed record WebhookDeliveryOutboxPayload(
    Guid WebhookEndpointId,
    Guid ApiClientId,
    string EventType,
    Guid AggregateId,
    string WebhookPayloadJson);
