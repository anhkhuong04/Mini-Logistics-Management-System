using System.Text.Json;
using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MiniLogistics.Application.Outbox;
using MiniLogistics.Application.PartnerApi;
using MiniLogistics.Domain.Outbox;
using MiniLogistics.Domain.PartnerApi;
using MiniLogistics.Infrastructure.Outbox;
using MiniLogistics.Infrastructure.PartnerApi;
using Xunit;

namespace MiniLogistics.Infrastructure.Tests;

public sealed class OutboxMessageDispatcherTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task DispatchDueAsync_CreatesWebhookDeliveryAndMarksOutboxSucceeded()
    {
        var message = CreateWebhookOutboxMessage();
        var outboxRepository = new FakeOutboxMessageRepository([message]);
        var deliveryRepository = new FakeWebhookDeliveryRepository([]);
        var dispatcher = CreateDispatcher(outboxRepository, deliveryRepository);

        await dispatcher.DispatchDueAsync();

        Assert.Equal(OutboxMessageStatus.Succeeded, message.Status);
        Assert.NotNull(message.ProcessedAtUtc);
        Assert.Null(message.NextAttemptAtUtc);
        Assert.Single(deliveryRepository.Deliveries);
        Assert.Equal(message.Id, deliveryRepository.Deliveries.Single().Id);
        Assert.Equal(1, outboxRepository.SaveChangesCount);
    }

    [Fact]
    public async Task DispatchDueAsync_WhenDeliveryExists_MarksSucceededWithoutDuplicate()
    {
        var message = CreateWebhookOutboxMessage();
        var payload = JsonSerializer.Deserialize<WebhookDeliveryOutboxPayload>(message.PayloadJson, JsonOptions)!;
        var existingDelivery = new WebhookDelivery(
            message.Id,
            payload.WebhookEndpointId,
            payload.ApiClientId,
            payload.EventType,
            payload.AggregateId,
            payload.WebhookPayloadJson,
            TestClock.UtcNow);
        var outboxRepository = new FakeOutboxMessageRepository([message]);
        var deliveryRepository = new FakeWebhookDeliveryRepository([existingDelivery]);
        var dispatcher = CreateDispatcher(outboxRepository, deliveryRepository);

        await dispatcher.DispatchDueAsync();

        Assert.Equal(OutboxMessageStatus.Succeeded, message.Status);
        Assert.Single(deliveryRepository.Deliveries);
        Assert.Equal(message.Id, deliveryRepository.Deliveries.Single().Id);
    }

    [Fact]
    public async Task DispatchDueAsync_WhenPayloadInvalid_MarksFailedForRetry()
    {
        var message = new OutboxMessage(
            Guid.NewGuid(),
            OutboxMessageTypes.WebhookShipmentCreated,
            Guid.NewGuid(),
            "{}",
            TestClock.UtcNow);
        var outboxRepository = new FakeOutboxMessageRepository([message]);
        var deliveryRepository = new FakeWebhookDeliveryRepository([]);
        var dispatcher = CreateDispatcher(outboxRepository, deliveryRepository);

        await dispatcher.DispatchDueAsync();

        Assert.Equal(OutboxMessageStatus.Failed, message.Status);
        Assert.Equal(1, message.RetryCount);
        Assert.NotNull(message.NextAttemptAtUtc);
        Assert.Empty(deliveryRepository.Deliveries);
    }

    [Fact]
    public async Task EndToEnd_OutboxToSignedReceiver_SucceedsWithSameEventId()
    {
        const string secret = "integration-secret-at-least-32-bytes";
        var apiClientId = Guid.NewGuid();
        var endpoint = new WebhookEndpoint(
            apiClientId,
            "https://partner.example.test/webhooks",
            secret,
            TestClock.UtcNow);
        var message = CreateWebhookOutboxMessage(endpoint);
        var outboxRepository = new FakeOutboxMessageRepository([message]);
        var deliveryRepository = new FakeWebhookDeliveryRepository([]);

        await CreateDispatcher(outboxRepository, deliveryRepository).DispatchDueAsync();

        var handler = new CapturingHttpMessageHandler();
        var webhookDispatcher = new WebhookDeliveryDispatcher(
            new HttpClient(handler),
            deliveryRepository,
            new FakeWebhookEndpointRepository(endpoint),
            new PassThroughSecretProtector(),
            new AllowWebhookUrlPolicy(),
            Options.Create(new WebhookSecurityOptions()),
            NullLogger<WebhookDeliveryDispatcher>.Instance,
            TestClock.Provider);
        await webhookDispatcher.DispatchDueAsync();

        var delivery = Assert.Single(deliveryRepository.Deliveries);
        Assert.Equal(message.Id, delivery.Id);
        Assert.Equal(WebhookDeliveryStatus.Succeeded, delivery.Status);
        Assert.NotNull(handler.Request);
        var timestamp = Assert.Single(handler.Request.Headers.GetValues("X-MiniLogistics-Timestamp"));
        var signature = Assert.Single(handler.Request.Headers.GetValues("X-MiniLogistics-Signature"));
        Assert.Equal(WebhookSignature.Compute(secret, timestamp, delivery.PayloadJson), signature);
    }

    private static OutboxMessageDispatcher CreateDispatcher(
        IOutboxMessageRepository outboxRepository,
        IWebhookDeliveryRepository deliveryRepository)
    {
        return new OutboxMessageDispatcher(
            outboxRepository,
            deliveryRepository,
            NullLogger<OutboxMessageDispatcher>.Instance,
            TestClock.Provider);
    }

    private static OutboxMessage CreateWebhookOutboxMessage(WebhookEndpoint? endpoint = null)
    {
        var eventId = Guid.NewGuid();
        var aggregateId = Guid.NewGuid();
        var webhookPayloadJson = JsonSerializer.Serialize(
            new WebhookShipmentPayload(
                eventId,
                WebhookEventTypes.ShipmentCreated,
                "MLG123456",
                "ECOM-10001",
                "PendingPickup",
                TestClock.UtcNow),
            JsonOptions);
        var outboxPayload = new WebhookDeliveryOutboxPayload(
            endpoint?.Id ?? Guid.NewGuid(),
            endpoint?.ApiClientId ?? Guid.NewGuid(),
            WebhookEventTypes.ShipmentCreated,
            aggregateId,
            webhookPayloadJson,
            endpoint?.ProtectedSigningSecret,
            endpoint?.SecretVersion ?? 1);

        return new OutboxMessage(
            eventId,
            OutboxMessageTypes.WebhookShipmentCreated,
            aggregateId,
            JsonSerializer.Serialize(outboxPayload, JsonOptions),
            TestClock.UtcNow);
    }

    private sealed class FakeOutboxMessageRepository : IOutboxMessageRepository
    {
        private readonly List<OutboxMessage> _messages;

        public FakeOutboxMessageRepository(IReadOnlyList<OutboxMessage> messages)
        {
            _messages = messages.ToList();
        }

        public int SaveChangesCount { get; private set; }

        public Task<IReadOnlyList<OutboxMessage>> GetDueAsync(
            DateTimeOffset dueAtUtc,
            int batchSize,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<OutboxMessage>>(_messages
                .Where(message =>
                    (message.Status == OutboxMessageStatus.Pending || message.Status == OutboxMessageStatus.Failed)
                    && message.NextAttemptAtUtc is not null
                    && message.NextAttemptAtUtc <= dueAtUtc)
                .Take(batchSize)
                .ToList());
        }

        public Task AddAsync(OutboxMessage message, CancellationToken cancellationToken = default)
        {
            _messages.Add(message);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveChangesCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeWebhookDeliveryRepository : IWebhookDeliveryRepository
    {
        private readonly List<WebhookDelivery> _deliveries;

        public FakeWebhookDeliveryRepository(IReadOnlyList<WebhookDelivery> deliveries)
        {
            _deliveries = deliveries.ToList();
        }

        public IReadOnlyList<WebhookDelivery> Deliveries => _deliveries.AsReadOnly();

        public Task<bool> ExistsAsync(Guid deliveryId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_deliveries.Any(delivery => delivery.Id == deliveryId));
        }

        public Task<IReadOnlyList<WebhookDelivery>> GetDueAsync(
            DateTimeOffset dueAtUtc,
            int batchSize,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<WebhookDelivery>>(_deliveries
                .Where(delivery => delivery.Status is WebhookDeliveryStatus.Pending or WebhookDeliveryStatus.Failed
                    && delivery.NextAttemptAtUtc is not null
                    && delivery.NextAttemptAtUtc <= dueAtUtc)
                .Take(batchSize)
                .ToList());
        }

        public Task<IReadOnlyList<WebhookDelivery>> GetRecentByApiClientIdsAsync(
            IReadOnlyCollection<Guid> apiClientIds,
            int takePerClient,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<WebhookDelivery>>([]);
        }

        public Task AddAsync(WebhookDelivery delivery, CancellationToken cancellationToken = default)
        {
            _deliveries.Add(delivery);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeWebhookEndpointRepository(WebhookEndpoint endpoint)
        : IWebhookEndpointRepository
    {
        public Task<IReadOnlyList<WebhookEndpoint>> GetActiveByApiClientIdAsync(
            Guid apiClientId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<WebhookEndpoint>>(
                endpoint.ApiClientId == apiClientId && endpoint.IsActive ? [endpoint] : []);

        public Task<IReadOnlyList<WebhookEndpoint>> GetByApiClientIdsAsync(
            IReadOnlyCollection<Guid> apiClientIds,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<WebhookEndpoint>>(
                apiClientIds.Contains(endpoint.ApiClientId) ? [endpoint] : []);

        public Task<WebhookEndpoint?> GetByIdAsync(
            Guid endpointId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<WebhookEndpoint?>(endpoint.Id == endpointId ? endpoint : null);

        public Task<WebhookEndpoint?> GetLatestByApiClientIdAsync(
            Guid apiClientId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<WebhookEndpoint?>(endpoint.ApiClientId == apiClientId ? endpoint : null);

        public Task AddAsync(WebhookEndpoint value, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class PassThroughSecretProtector : ISecretProtector
    {
        public string Protect(string plaintextSecret) => plaintextSecret;

        public string Unprotect(string protectedSecret) => protectedSecret;

        public bool IsProtected(string value) => true;
    }

    private sealed class AllowWebhookUrlPolicy : IWebhookUrlPolicy
    {
        public Task<MiniLogistics.Domain.Common.Result> ValidateAsync(
            string url,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(MiniLogistics.Domain.Common.Result.Success());
    }

    private sealed class CapturingHttpMessageHandler : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
        }
    }
}
