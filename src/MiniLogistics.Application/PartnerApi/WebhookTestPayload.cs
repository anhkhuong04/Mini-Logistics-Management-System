namespace MiniLogistics.Application.PartnerApi;

public sealed record WebhookTestPayload(
    Guid EventId,
    string Event,
    string Message,
    DateTimeOffset CreatedAtUtc);
