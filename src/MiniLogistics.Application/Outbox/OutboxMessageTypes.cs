namespace MiniLogistics.Application.Outbox;

public static class OutboxMessageTypes
{
    public const string WebhookShipmentCreated = nameof(WebhookShipmentCreated);
    public const string WebhookShipmentStatusChanged = nameof(WebhookShipmentStatusChanged);
}
