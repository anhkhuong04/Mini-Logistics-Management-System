using MiniLogistics.Domain.Shipments;
using MiniLogistics.Domain.PartnerApi;

namespace MiniLogistics.Application.PartnerApi;

public sealed class NullWebhookEventPublisher : IWebhookEventPublisher
{
    public static readonly NullWebhookEventPublisher Instance = new();

    private NullWebhookEventPublisher()
    {
    }

    public Task PublishShipmentAsync(
        Shipment shipment,
        string eventType,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task PublishShipmentAsync(
        Shipment shipment,
        ExternalShipmentReference reference,
        string eventType,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
