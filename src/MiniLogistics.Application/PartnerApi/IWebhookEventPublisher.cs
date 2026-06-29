using MiniLogistics.Domain.Shipments;

namespace MiniLogistics.Application.PartnerApi;

public interface IWebhookEventPublisher
{
    Task PublishShipmentAsync(
        Shipment shipment,
        string eventType,
        CancellationToken cancellationToken = default);
}
