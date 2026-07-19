namespace MiniLogistics.Domain.PartnerApi;

/// <summary>
/// Defines the supported Webhook Delivery Status values in the domain model.
/// </summary>
public enum WebhookDeliveryStatus
{
    Pending = 0,
    Succeeded = 1,
    Failed = 2
}
