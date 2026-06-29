using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Logging;
using MiniLogistics.Application.PartnerApi;
using MiniLogistics.Domain.PartnerApi;

namespace MiniLogistics.Infrastructure.PartnerApi;

public sealed class WebhookDeliveryDispatcher
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

    private readonly HttpClient _httpClient;
    private readonly IWebhookDeliveryRepository _webhookDeliveryRepository;
    private readonly IWebhookEndpointRepository _webhookEndpointRepository;
    private readonly ILogger<WebhookDeliveryDispatcher> _logger;

    public WebhookDeliveryDispatcher(
        HttpClient httpClient,
        IWebhookDeliveryRepository webhookDeliveryRepository,
        IWebhookEndpointRepository webhookEndpointRepository,
        ILogger<WebhookDeliveryDispatcher> logger)
    {
        _httpClient = httpClient;
        _webhookDeliveryRepository = webhookDeliveryRepository;
        _webhookEndpointRepository = webhookEndpointRepository;
        _logger = logger;
    }

    public async Task DispatchDueAsync(CancellationToken cancellationToken = default)
    {
        var deliveries = await _webhookDeliveryRepository.GetDueAsync(
            DateTimeOffset.UtcNow,
            BatchSize,
            cancellationToken);

        foreach (var delivery in deliveries)
        {
            await DispatchAsync(delivery, cancellationToken);
        }

        if (deliveries.Count > 0)
        {
            await _webhookDeliveryRepository.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task DispatchAsync(WebhookDelivery delivery, CancellationToken cancellationToken)
    {
        var endpoint = await _webhookEndpointRepository.GetByIdAsync(
            delivery.WebhookEndpointId,
            cancellationToken);
        if (endpoint is null || !endpoint.IsActive)
        {
            MarkFailed(
                delivery,
                null,
                "Webhook endpoint is inactive or missing.",
                DateTimeOffset.UtcNow,
                retry: false);
            return;
        }

        var attemptedAtUtc = DateTimeOffset.UtcNow;
        try
        {
            using var request = BuildRequest(delivery, endpoint, attemptedAtUtc);
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var statusCode = (int)response.StatusCode;

            if (response.IsSuccessStatusCode)
            {
                delivery.MarkSucceeded(statusCode, attemptedAtUtc);
                _logger.LogInformation(
                    "Webhook delivery {DeliveryId} succeeded with status {StatusCode}.",
                    delivery.Id,
                    statusCode);
                return;
            }

            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
            MarkFailed(
                delivery,
                statusCode,
                string.IsNullOrWhiteSpace(responseText)
                    ? $"Webhook endpoint returned HTTP {statusCode}."
                    : responseText,
                attemptedAtUtc,
                retry: true);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            MarkFailed(delivery, null, exception.Message, attemptedAtUtc, retry: true);
        }
    }

    private static HttpRequestMessage BuildRequest(
        WebhookDelivery delivery,
        WebhookEndpoint endpoint,
        DateTimeOffset attemptedAtUtc)
    {
        var timestamp = attemptedAtUtc.ToString("O");
        var signature = WebhookSignature.Compute(
            endpoint.SigningSecret,
            timestamp,
            delivery.PayloadJson);
        var request = new HttpRequestMessage(HttpMethod.Post, endpoint.Url)
        {
            Content = new StringContent(delivery.PayloadJson, Encoding.UTF8, "application/json")
        };

        request.Headers.Add("X-MiniLogistics-Event", delivery.EventType);
        request.Headers.Add("X-MiniLogistics-Signature", signature);
        request.Headers.Add("X-MiniLogistics-Timestamp", timestamp);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        return request;
    }

    private void MarkFailed(
        WebhookDelivery delivery,
        int? statusCode,
        string error,
        DateTimeOffset attemptedAtUtc,
        bool retry)
    {
        var nextAttemptAtUtc = retry
            ? CalculateNextAttempt(delivery.RetryCount + 1, attemptedAtUtc)
            : null;
        delivery.MarkFailed(statusCode, error, attemptedAtUtc, nextAttemptAtUtc);

        _logger.LogWarning(
            "Webhook delivery {DeliveryId} failed on attempt {Attempt} with status {StatusCode}. Next attempt: {NextAttemptAtUtc}. Error: {Error}",
            delivery.Id,
            delivery.RetryCount,
            statusCode,
            nextAttemptAtUtc,
            delivery.LastError);
    }

    private static DateTimeOffset? CalculateNextAttempt(int failedAttemptNumber, DateTimeOffset attemptedAtUtc)
    {
        return failedAttemptNumber >= MaxAttempts
            ? null
            : attemptedAtUtc + BackoffSchedule[failedAttemptNumber - 1];
    }
}
