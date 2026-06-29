namespace MiniLogistics.Application.PartnerApi;

public static class WebhookEventTypes
{
    public const string WebhookTest = "webhook.test";
    public const string ShipmentCreated = "shipment.created";
    public const string ShipmentStatusChanged = "shipment.status_changed";
}
