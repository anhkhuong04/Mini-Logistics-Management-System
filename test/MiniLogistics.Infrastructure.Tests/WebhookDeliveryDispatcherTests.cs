using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using MiniLogistics.Application.PartnerApi;
using MiniLogistics.Domain.PartnerApi;
using MiniLogistics.Infrastructure.PartnerApi;
using Xunit;

namespace MiniLogistics.Infrastructure.Tests;

public sealed class WebhookDeliveryDispatcherTests
{
    [Fact]
    public async Task DispatchDueAsync_WhenEndpointReturnsSuccess_MarksDeliverySucceededAndSignsRequest()
    {
        var apiClientId = Guid.NewGuid();
        var endpoint = new WebhookEndpoint(
            apiClientId,
            "https://partner.example.test/webhooks",
            FakeSecretProtector.ProtectValue("secret"),
            TestClock.UtcNow);
        var delivery = CreateDelivery(endpoint, apiClientId);
        var endpointRepository = new FakeWebhookEndpointRepository([endpoint]);
        var deliveryRepository = new FakeWebhookDeliveryRepository([delivery]);
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NoContent));
        var dispatcher = CreateDispatcher(handler, deliveryRepository, endpointRepository);

        await dispatcher.DispatchDueAsync();

        Assert.Equal(WebhookDeliveryStatus.Succeeded, delivery.Status);
        Assert.Equal(204, delivery.LastResponseStatusCode);
        Assert.Null(delivery.LastError);
        Assert.Null(delivery.NextAttemptAtUtc);
        Assert.Equal(1, deliveryRepository.SaveChangesCount);
        Assert.NotNull(handler.LastRequest);
        Assert.True(handler.LastRequest.Headers.Contains("X-MiniLogistics-Event"));
        Assert.True(handler.LastRequest.Headers.Contains("X-MiniLogistics-Signature"));
        Assert.True(handler.LastRequest.Headers.Contains("X-MiniLogistics-Timestamp"));
        var timestamp = Assert.Single(handler.LastRequest.Headers.GetValues("X-MiniLogistics-Timestamp"));
        var signature = Assert.Single(handler.LastRequest.Headers.GetValues("X-MiniLogistics-Signature"));
        Assert.Equal(WebhookSignature.Compute("secret", timestamp, delivery.PayloadJson), signature);
    }

    [Fact]
    public async Task DispatchDueAsync_WhenEndpointReturnsFailure_SchedulesRetryAndLogsResult()
    {
        var apiClientId = Guid.NewGuid();
        var endpoint = new WebhookEndpoint(
            apiClientId,
            "https://partner.example.test/webhooks",
            FakeSecretProtector.ProtectValue("secret"),
            TestClock.UtcNow);
        var delivery = CreateDelivery(endpoint, apiClientId);
        var endpointRepository = new FakeWebhookEndpointRepository([endpoint]);
        var deliveryRepository = new FakeWebhookDeliveryRepository([delivery]);
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("temporary failure")
        });
        var beforeDispatch = TestClock.UtcNow;
        var dispatcher = CreateDispatcher(handler, deliveryRepository, endpointRepository);

        await dispatcher.DispatchDueAsync();

        Assert.Equal(WebhookDeliveryStatus.Failed, delivery.Status);
        Assert.Equal(1, delivery.RetryCount);
        Assert.Equal(500, delivery.LastResponseStatusCode);
        Assert.Equal("temporary failure", delivery.LastError);
        Assert.NotNull(delivery.LastAttemptAtUtc);
        Assert.NotNull(delivery.NextAttemptAtUtc);
        Assert.True(delivery.NextAttemptAtUtc >= beforeDispatch.AddMinutes(1));
        Assert.Equal(1, deliveryRepository.SaveChangesCount);
    }

    private static WebhookDeliveryDispatcher CreateDispatcher(
        HttpMessageHandler handler,
        IWebhookDeliveryRepository deliveryRepository,
        IWebhookEndpointRepository endpointRepository)
    {
        return new WebhookDeliveryDispatcher(
            new HttpClient(handler),
            deliveryRepository,
            endpointRepository,
            new FakeSecretProtector(),
            NullLogger<WebhookDeliveryDispatcher>.Instance,
            TestClock.Provider);
    }

    private static WebhookDelivery CreateDelivery(WebhookEndpoint endpoint, Guid apiClientId)
    {
        return new WebhookDelivery(
            Guid.NewGuid(),
            endpoint.Id,
            apiClientId,
            WebhookEventTypes.ShipmentStatusChanged,
            Guid.NewGuid(),
            "{\"event\":\"shipment.status_changed\"}",
            TestClock.UtcNow);
    }

    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFactory;

        public FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(_responseFactory(request));
        }
    }

    private sealed class FakeWebhookEndpointRepository : IWebhookEndpointRepository
    {
        private readonly List<WebhookEndpoint> _endpoints;

        public FakeWebhookEndpointRepository(IReadOnlyList<WebhookEndpoint> endpoints)
        {
            _endpoints = endpoints.ToList();
        }

        public Task<IReadOnlyList<WebhookEndpoint>> GetActiveByApiClientIdAsync(
            Guid apiClientId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<WebhookEndpoint>>(_endpoints
                .Where(endpoint => endpoint.ApiClientId == apiClientId && endpoint.IsActive)
                .ToList());
        }

        public Task<IReadOnlyList<WebhookEndpoint>> GetByApiClientIdsAsync(
            IReadOnlyCollection<Guid> apiClientIds,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<WebhookEndpoint>>(_endpoints
                .Where(endpoint => apiClientIds.Contains(endpoint.ApiClientId))
                .ToList());
        }

        public Task<WebhookEndpoint?> GetByIdAsync(
            Guid endpointId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_endpoints.FirstOrDefault(endpoint => endpoint.Id == endpointId));
        }

        public Task<WebhookEndpoint?> GetLatestByApiClientIdAsync(
            Guid apiClientId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_endpoints
                .Where(endpoint => endpoint.ApiClientId == apiClientId)
                .OrderByDescending(endpoint => endpoint.IsActive)
                .ThenByDescending(endpoint => endpoint.UpdatedAtUtc ?? endpoint.CreatedAtUtc)
                .FirstOrDefault());
        }

        public Task AddAsync(WebhookEndpoint endpoint, CancellationToken cancellationToken = default)
        {
            _endpoints.Add(endpoint);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
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

        public int SaveChangesCount { get; private set; }

        public Task<bool> ExistsAsync(
            Guid deliveryId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_deliveries.Any(delivery => delivery.Id == deliveryId));
        }

        public Task<IReadOnlyList<WebhookDelivery>> GetDueAsync(
            DateTimeOffset dueAtUtc,
            int batchSize,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<WebhookDelivery>>(_deliveries
                .Where(delivery =>
                    delivery.Status != WebhookDeliveryStatus.Succeeded
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
            return Task.FromResult<IReadOnlyList<WebhookDelivery>>(_deliveries
                .Where(delivery => apiClientIds.Contains(delivery.ApiClientId))
                .GroupBy(delivery => delivery.ApiClientId)
                .SelectMany(group => group
                    .OrderByDescending(delivery => delivery.CreatedAtUtc)
                    .Take(takePerClient))
                .ToList());
        }

        public Task AddAsync(WebhookDelivery delivery, CancellationToken cancellationToken = default)
        {
            _deliveries.Add(delivery);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveChangesCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeSecretProtector : ISecretProtector
    {
        private const string Prefix = "fake:v1:";

        public static string ProtectValue(string plaintextSecret)
        {
            return Prefix + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(plaintextSecret));
        }

        public string Protect(string plaintextSecret)
        {
            return ProtectValue(plaintextSecret);
        }

        public string Unprotect(string protectedSecret)
        {
            return IsProtected(protectedSecret)
                ? System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(protectedSecret[Prefix.Length..]))
                : protectedSecret;
        }

        public bool IsProtected(string value)
        {
            return value.StartsWith(Prefix, StringComparison.Ordinal);
        }
    }
}
