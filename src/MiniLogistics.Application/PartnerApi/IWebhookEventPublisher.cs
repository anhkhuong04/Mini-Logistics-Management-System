using MiniLogistics.Domain.Shipments;
using MiniLogistics.Domain.PartnerApi;

namespace MiniLogistics.Application.PartnerApi;

public interface IWebhookEventPublisher
{
    Task PublishShipmentAsync(
        Shipment shipment,
        string eventType,
        CancellationToken cancellationToken = default);

    Task PublishShipmentAsync(
        Shipment shipment,
        ExternalShipmentReference reference,
        string eventType,
        CancellationToken cancellationToken = default);
}
