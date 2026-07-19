using System.Text.Json;
using Microsoft.Extensions.Logging;
using MiniLogistics.Application.Outbox;
using MiniLogistics.Application.PartnerApi;
using MiniLogistics.Domain.Outbox;
using MiniLogistics.Domain.PartnerApi;

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

    private static readonly JsonSerializerOptions PayloadJsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IOutboxMessageRepository _outboxMessageRepository;
    private readonly IWebhookDeliveryRepository _webhookDeliveryRepository;
    private readonly ILogger<OutboxMessageDispatcher> _logger;
    private readonly TimeProvider _timeProvider;

    public OutboxMessageDispatcher(
        IOutboxMessageRepository outboxMessageRepository,
        IWebhookDeliveryRepository webhookDeliveryRepository,
        ILogger<OutboxMessageDispatcher> logger,
        TimeProvider timeProvider)
    {
        _outboxMessageRepository = outboxMessageRepository;
        _webhookDeliveryRepository = webhookDeliveryRepository;
        _logger = logger;
        _timeProvider = timeProvider;
    }

    public async Task DispatchDueAsync(CancellationToken cancellationToken = default)
    {
        var messages = await _outboxMessageRepository.GetDueAsync(
            _timeProvider.GetUtcNow(),
            BatchSize,
            cancellationToken);

        foreach (var message in messages)
        {
            await DispatchAsync(message, cancellationToken);
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
        message.MarkProcessing(_timeProvider.GetUtcNow());

        try
        {
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
                    _timeProvider.GetUtcNow());

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

    private static DateTimeOffset? CalculateNextAttempt(
        int failedAttemptNumber,
        DateTimeOffset failedAtUtc)
    {
        return failedAttemptNumber >= MaxAttempts
            ? null
            : failedAtUtc + BackoffSchedule[failedAttemptNumber - 1];
    }
}
