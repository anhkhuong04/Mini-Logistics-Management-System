using System.Text.Json;
using Microsoft.Extensions.Logging;
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
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<WebhookEventPublisher>? _logger;

    public WebhookEventPublisher(
        IExternalShipmentReferenceRepository externalShipmentReferenceRepository,
        IWebhookEndpointRepository webhookEndpointRepository,
        IOutboxWriter outboxWriter,
        TimeProvider timeProvider,
        ILogger<WebhookEventPublisher>? logger = null)
    {
        _externalShipmentReferenceRepository = externalShipmentReferenceRepository;
        _webhookEndpointRepository = webhookEndpointRepository;
        _outboxWriter = outboxWriter;
        _timeProvider = timeProvider;
        _logger = logger;
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
            _logger?.LogDebug(
                "Webhook event {EventType} skipped for shipment {ShipmentId} because no external reference exists",
                eventType,
                shipment.Id);
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
            _logger?.LogInformation(
                "Webhook event {EventType} skipped for shipment {ShipmentId} because API client {ApiClientId} has no active endpoints",
                eventType,
                shipment.Id,
                reference.ApiClientId);
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
                JsonSerializer.Serialize(outboxPayload, PayloadJsonOptions),
                _timeProvider.GetUtcNow());

            await _outboxWriter.AddAsync(outboxMessage, cancellationToken);
            _logger?.LogInformation(
                "Queued webhook event {EventType} with event id {EventId} for shipment {ShipmentId}, API client {ApiClientId}, endpoint {WebhookEndpointId}",
                eventType,
                eventId,
                shipment.Id,
                reference.ApiClientId,
                endpoint.Id);
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
