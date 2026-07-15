using System.Text.Json;
using MiniLogistics.Application.Outbox;
using MiniLogistics.Domain.Outbox;
using MiniLogistics.Domain.Shipments;
using MiniLogistics.Domain.PartnerApi;

namespace MiniLogistics.Application.PartnerApi;

public sealed class WebhookEventPublisher : IWebhookEventPublisher
{
    private static readonly JsonSerializerOptions PayloadJsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IExternalShipmentReferenceRepository _externalShipmentReferenceRepository;
    private readonly IWebhookEndpointRepository _webhookEndpointRepository;
    private readonly IOutboxWriter _outboxWriter;

    public WebhookEventPublisher(
        IExternalShipmentReferenceRepository externalShipmentReferenceRepository,
        IWebhookEndpointRepository webhookEndpointRepository,
        IOutboxWriter outboxWriter)
    {
        _externalShipmentReferenceRepository = externalShipmentReferenceRepository;
        _webhookEndpointRepository = webhookEndpointRepository;
        _outboxWriter = outboxWriter;
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

        await PublishShipmentAsync(shipment, reference, eventType, cancellationToken);
    }

    public async Task PublishShipmentAsync(
        Shipment shipment,
        ExternalShipmentReference reference,
        string eventType,
        CancellationToken cancellationToken = default)
    {
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
            var outboxPayload = new WebhookDeliveryOutboxPayload(
                endpoint.Id,
                reference.ApiClientId,
                eventType,
                shipment.Id,
                JsonSerializer.Serialize(payload, PayloadJsonOptions));
            var outboxMessage = new OutboxMessage(
                eventId,
                ToOutboxMessageType(eventType),
                shipment.Id,
                JsonSerializer.Serialize(outboxPayload, PayloadJsonOptions));

            await _outboxWriter.AddAsync(outboxMessage, cancellationToken);
        }
    }

    private static string ToOutboxMessageType(string eventType)
    {
        return eventType switch
        {
            WebhookEventTypes.ShipmentCreated => OutboxMessageTypes.WebhookShipmentCreated,
            WebhookEventTypes.ShipmentStatusChanged => OutboxMessageTypes.WebhookShipmentStatusChanged,
            _ => throw new InvalidOperationException($"Unsupported webhook event type '{eventType}'.")
        };
    }
}
