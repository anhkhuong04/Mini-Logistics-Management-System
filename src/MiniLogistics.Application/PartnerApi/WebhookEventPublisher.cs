using System.Text.Json;
using MiniLogistics.Domain.Shipments;
using MiniLogistics.Domain.PartnerApi;

namespace MiniLogistics.Application.PartnerApi;

public sealed class WebhookEventPublisher : IWebhookEventPublisher
{
    private static readonly JsonSerializerOptions PayloadJsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IExternalShipmentReferenceRepository _externalShipmentReferenceRepository;
    private readonly IWebhookEndpointRepository _webhookEndpointRepository;
    private readonly IWebhookDeliveryRepository _webhookDeliveryRepository;

    public WebhookEventPublisher(
        IExternalShipmentReferenceRepository externalShipmentReferenceRepository,
        IWebhookEndpointRepository webhookEndpointRepository,
        IWebhookDeliveryRepository webhookDeliveryRepository)
    {
        _externalShipmentReferenceRepository = externalShipmentReferenceRepository;
        _webhookEndpointRepository = webhookEndpointRepository;
        _webhookDeliveryRepository = webhookDeliveryRepository;
    }

    public async Task PublishShipmentAsync(
        Shipment shipment,
        string eventType,
        CancellationToken cancellationToken = default)
    {
        var reference = await _externalShipmentReferenceRepository.GetByShipmentIdAsync(
            shipment.Id,
            cancellationToken);
        if (reference is null)
        {
            return;
        }

        var endpoints = await _webhookEndpointRepository.GetActiveByApiClientIdAsync(
            reference.ApiClientId,
            cancellationToken);
        if (endpoints.Count == 0)
        {
            return;
        }

        var lastStatusHistory = shipment.StatusHistory
            .OrderByDescending(history => history.ChangedAtUtc)
            .FirstOrDefault();
        var changedAtUtc = lastStatusHistory?.ChangedAtUtc ?? shipment.UpdatedAtUtc ?? shipment.CreatedAtUtc;

        foreach (var endpoint in endpoints)
        {
            var eventId = Guid.NewGuid();
            var payload = new WebhookShipmentPayload(
                eventId,
                eventType,
                shipment.TrackingCode.Value,
                reference.ExternalOrderId,
                shipment.Status.ToString(),
                changedAtUtc);
            var delivery = new WebhookDelivery(
                eventId,
                endpoint.Id,
                reference.ApiClientId,
                eventType,
                shipment.Id,
                JsonSerializer.Serialize(payload, PayloadJsonOptions));

            await _webhookDeliveryRepository.AddAsync(delivery, cancellationToken);
        }

        await _webhookDeliveryRepository.SaveChangesAsync(cancellationToken);
    }
}
