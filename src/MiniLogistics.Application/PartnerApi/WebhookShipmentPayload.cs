namespace MiniLogistics.Application.PartnerApi;

public sealed record WebhookShipmentPayload(
    Guid EventId,
    string Event,
    string TrackingCode,
    string ExternalOrderId,
    string Status,
    DateTimeOffset ChangedAtUtc);
