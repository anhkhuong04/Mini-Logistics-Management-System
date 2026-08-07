using System.Text.Json;
using Microsoft.Extensions.Logging;
using MiniLogistics.Application.Outbox;
using MiniLogistics.Application.PartnerApi;
using MiniLogistics.Application.Shops;
using MiniLogistics.Application.Shops.Notifications;
using MiniLogistics.Domain.Outbox;
using MiniLogistics.Domain.PartnerApi;
using MiniLogistics.Domain.Shops;
using MiniLogistics.Infrastructure.PartnerApi;

namespace MiniLogistics.Infrastructure.Outbox;

public sealed class OutboxMessageDispatcher
{
    private static readonly TimeSpan[] BackoffSchedule =
    [
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(15),
        TimeSpan.FromHours(1),
        TimeSpan.FromHours(6)
    ];

    private const int BatchSize = 20;
    private const int MaxAttempts = 5;
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(5);

    private static readonly JsonSerializerOptions PayloadJsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IOutboxMessageRepository _outboxMessageRepository;
    private readonly IWebhookDeliveryRepository _webhookDeliveryRepository;
    private readonly IShopNotificationRepository? _shopNotificationRepository;
    private readonly IShopRepository? _shopRepository;
    private readonly ILogger<OutboxMessageDispatcher> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly string _workerId = $"{Environment.MachineName}:{Environment.ProcessId}:{Guid.NewGuid():N}";

    public OutboxMessageDispatcher(
        IOutboxMessageRepository outboxMessageRepository,
        IWebhookDeliveryRepository webhookDeliveryRepository,
        ILogger<OutboxMessageDispatcher> logger,
        TimeProvider timeProvider,
        IShopNotificationRepository? shopNotificationRepository = null,
        IShopRepository? shopRepository = null)
    {
        _outboxMessageRepository = outboxMessageRepository;
        _webhookDeliveryRepository = webhookDeliveryRepository;
        _shopNotificationRepository = shopNotificationRepository;
        _shopRepository = shopRepository;
        _logger = logger;
        _timeProvider = timeProvider;
    }

    public async Task DispatchDueAsync(CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow();
        var messages = await _outboxMessageRepository.ClaimDueAsync(
            _workerId,
            now,
            now + LeaseDuration,
            BatchSize,
            cancellationToken);

        foreach (var message in messages)
        {
            PartnerApiWorkerTelemetry.RecordClaim("outbox", message.CreatedAtUtc, now);
            await DispatchAsync(message, cancellationToken);
            PartnerApiWorkerTelemetry.RecordResult("outbox", message.Status.ToString());
        }

        if (messages.Count > 0)
        {
            await _outboxMessageRepository.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task DispatchAsync(
        OutboxMessage message,
        CancellationToken cancellationToken)
    {
        try
        {
            if (message.Type == OutboxMessageTypes.ShopNotification)
            {
                await DispatchShopNotificationAsync(message, cancellationToken);
                message.MarkSucceeded(_timeProvider.GetUtcNow());
                return;
            }

            if (message.Type is not (OutboxMessageTypes.WebhookShipmentCreated or OutboxMessageTypes.WebhookShipmentStatusChanged))
            {
                throw new InvalidOperationException($"Unsupported outbox message type '{message.Type}'.");
            }

            var payload = JsonSerializer.Deserialize<WebhookDeliveryOutboxPayload>(
                message.PayloadJson,
                PayloadJsonOptions);
            if (payload is null)
            {
                throw new InvalidOperationException("Webhook outbox payload is invalid.");
            }

            if (!await _webhookDeliveryRepository.ExistsAsync(message.Id, cancellationToken))
            {
                var delivery = new WebhookDelivery(
                    message.Id,
                    payload.WebhookEndpointId,
                    payload.ApiClientId,
                    payload.EventType,
                    payload.AggregateId,
                    payload.WebhookPayloadJson,
                    _timeProvider.GetUtcNow(),
                    protectedSigningSecret: payload.ProtectedSigningSecret,
                    secretVersion: payload.SecretVersion);

                await _webhookDeliveryRepository.AddAsync(delivery, cancellationToken);
            }

            message.MarkSucceeded(_timeProvider.GetUtcNow());
        }
        catch (Exception exception)
        {
            var now = _timeProvider.GetUtcNow();
            message.MarkFailed(exception.Message, CalculateNextAttempt(message.RetryCount + 1, now), now);
            _logger.LogWarning(
                exception,
                "Outbox message {OutboxMessageId} failed on attempt {Attempt}. Next attempt: {NextAttemptAtUtc}.",
                message.Id,
                message.RetryCount,
                message.NextAttemptAtUtc);
        }
    }

    private async Task DispatchShopNotificationAsync(
        OutboxMessage message,
        CancellationToken cancellationToken)
    {
        if (_shopNotificationRepository is null || _shopRepository is null)
        {
            throw new InvalidOperationException("Shop notification dispatch dependencies are not configured.");
        }

        var payload = JsonSerializer.Deserialize<ShopNotificationOutboxPayload>(
            message.PayloadJson,
            PayloadJsonOptions)
            ?? throw new InvalidOperationException("Shop notification outbox payload is invalid.");
        var shop = await _shopRepository.GetByIdAsync(payload.ShopId, cancellationToken)
            ?? throw new InvalidOperationException($"Shop '{payload.ShopId}' was not found.");
        var preference = await _shopNotificationRepository.GetPreferenceAsync(
            shop.Id,
            shop.OwnerUserId,
            cancellationToken);
        if (preference is not null && !preference.IsEnabled(payload.EventType))
        {
            return;
        }

        if (await _shopNotificationRepository.ExistsAsync(message.Id, cancellationToken))
        {
            return;
        }

        var (title, notificationMessage) = BuildNotificationContent(payload);
        await _shopNotificationRepository.AddAsync(
            new ShopNotification(
                message.Id,
                shop.Id,
                shop.OwnerUserId,
                payload.EventType,
                title,
                notificationMessage,
                _timeProvider.GetUtcNow(),
                payload.ShipmentId),
            cancellationToken);
    }

    private static (string Title, string Message) BuildNotificationContent(ShopNotificationOutboxPayload payload)
    {
        return payload.EventType switch
        {
            ShopNotificationEventTypes.Assigned => ("Shipment assigned", $"Shipment {payload.TrackingCode} was assigned to a shipper."),
            ShopNotificationEventTypes.PickedUp => ("Shipment picked up", $"Shipment {payload.TrackingCode} was picked up."),
            ShopNotificationEventTypes.Delivered => ("Shipment delivered", $"Shipment {payload.TrackingCode} was delivered."),
            ShopNotificationEventTypes.DeliveryFailed => ("Delivery failed", $"Delivery failed for shipment {payload.TrackingCode}."),
            ShopNotificationEventTypes.Returned => ("Shipment returned", $"Shipment {payload.TrackingCode} was returned."),
            ShopNotificationEventTypes.CodCollected => ("COD collected", $"COD was collected for shipment {payload.TrackingCode}."),
            ShopNotificationEventTypes.CodSettled => ("COD settled", $"COD was settled for shipment {payload.TrackingCode}."),
            _ => throw new InvalidOperationException($"Unsupported shop notification event '{payload.EventType}'.")
        };
    }

    private static DateTimeOffset? CalculateNextAttempt(
        int failedAttemptNumber,
        DateTimeOffset failedAtUtc)
    {
        return failedAttemptNumber >= MaxAttempts
            ? null
            : failedAtUtc + BackoffSchedule[failedAttemptNumber - 1];
    }
}
