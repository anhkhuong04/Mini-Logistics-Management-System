using System.Text.Json;
using System.Text;
using MiniLogistics.Application.AdminAuditing;
using MiniLogistics.Application.Common;
using MiniLogistics.Application.Outbox;
using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.PartnerApi;

namespace MiniLogistics.Application.PartnerApi;

public sealed class WebhookManagementService : IWebhookManagementService
{
    private static readonly JsonSerializerOptions PayloadJsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IIntegrationScopeService _scopeService;
    private readonly PartnerIntegrationDashboardBuilder _dashboardBuilder;
    private readonly IWebhookEndpointRepository _webhookEndpointRepository;
    private readonly IWebhookDeliveryRepository _webhookDeliveryRepository;
    private readonly PartnerCredentialAuditWriter _credentialAuditWriter;
    private readonly ISecretProtector _secretProtector;
    private readonly IWebhookUrlPolicy _webhookUrlPolicy;
    private readonly IAdminAuditService _adminAuditService;
    private readonly IOutboxMessageRepository? _outboxMessageRepository;
    private readonly TimeProvider _timeProvider;

    public WebhookManagementService(
        IIntegrationScopeService scopeService,
        PartnerIntegrationDashboardBuilder dashboardBuilder,
        IWebhookEndpointRepository webhookEndpointRepository,
        IWebhookDeliveryRepository webhookDeliveryRepository,
        PartnerCredentialAuditWriter credentialAuditWriter,
        ISecretProtector secretProtector,
        IWebhookUrlPolicy webhookUrlPolicy,
        TimeProvider timeProvider,
        IAdminAuditService? adminAuditService = null,
        IOutboxMessageRepository? outboxMessageRepository = null)
    {
        _scopeService = scopeService;
        _dashboardBuilder = dashboardBuilder;
        _webhookEndpointRepository = webhookEndpointRepository;
        _webhookDeliveryRepository = webhookDeliveryRepository;
        _credentialAuditWriter = credentialAuditWriter;
        _secretProtector = secretProtector;
        _webhookUrlPolicy = webhookUrlPolicy;
        _timeProvider = timeProvider;
        _adminAuditService = adminAuditService ?? NullAdminAuditService.Instance;
        _outboxMessageRepository = outboxMessageRepository;
    }

    public async Task<Result<PartnerIntegrationDashboardResponse>> GetDashboardAsync(
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        return await _dashboardBuilder.GetAsync(currentUserId, cancellationToken);
    }

    public async Task<Result<PartnerWebhookEndpointResponse>> UpsertWebhookEndpointAsync(
        UpsertPartnerWebhookEndpointCommand command,
        CancellationToken cancellationToken = default)
    {
        var apiClientResult = await _scopeService.GetManageableApiClientAsync(
            command.CurrentUserId,
            command.ApiClientId,
            cancellationToken);
        if (apiClientResult.IsFailure)
        {
            return Result<PartnerWebhookEndpointResponse>.Failure(apiClientResult.Error);
        }

        if (!apiClientResult.Value.HasScope(PartnerApiScope.WebhookManage))
        {
            return Result<PartnerWebhookEndpointResponse>.Failure(PartnerApiErrors.MissingScope);
        }

        if (string.IsNullOrWhiteSpace(command.SigningSecret)
            || Encoding.UTF8.GetByteCount(command.SigningSecret) < 32)
        {
            var validationError = ApplicationErrors.ValidationFailed(
                "Webhook signing secret must contain at least 32 bytes.");
            await _credentialAuditWriter.SaveAsync(
                command.CurrentUserId,
                apiClientResult.Value.ShopId,
                apiClientResult.Value.Id,
                PartnerApiCredentialAuditActions.WebhookEndpointUpserted,
                isSuccess: false,
                validationError,
                cancellationToken);

            return Result<PartnerWebhookEndpointResponse>.Failure(validationError);
        }

        var urlValidationResult = await _webhookUrlPolicy.ValidateAsync(command.Url, cancellationToken);
        if (urlValidationResult.IsFailure)
        {
            await _credentialAuditWriter.SaveAsync(
                command.CurrentUserId,
                apiClientResult.Value.ShopId,
                apiClientResult.Value.Id,
                PartnerApiCredentialAuditActions.WebhookEndpointUpserted,
                isSuccess: false,
                urlValidationResult.Error,
                cancellationToken);

            return Result<PartnerWebhookEndpointResponse>.Failure(urlValidationResult.Error);
        }

        try
        {
            var protectedSigningSecret = _secretProtector.Protect(command.SigningSecret);
            var endpoint = await _webhookEndpointRepository.GetLatestByApiClientIdAsync(
                apiClientResult.Value.Id,
                cancellationToken);
            var oldEndpointValue = endpoint is null
                ? null
                : new
                {
                    Url = GetAuditUrl(endpoint.Url),
                    endpoint.IsActive
                };
            var now = _timeProvider.GetUtcNow();
            if (endpoint is null)
            {
                endpoint = new WebhookEndpoint(apiClientResult.Value.Id, command.Url, protectedSigningSecret, now);
                await _webhookEndpointRepository.AddAsync(endpoint, cancellationToken);
            }
            else
            {
                endpoint.Update(command.Url, protectedSigningSecret, now);
                endpoint.Activate(now);
            }

            await _credentialAuditWriter.AddAsync(
                command.CurrentUserId,
                apiClientResult.Value.ShopId,
                apiClientResult.Value.Id,
                PartnerApiCredentialAuditActions.WebhookEndpointUpserted,
                isSuccess: true,
                error: null,
                cancellationToken);
            await _adminAuditService.RecordAsync(
                new AdminAuditEntry(
                    command.CurrentUserId,
                    AdminAuditActions.PartnerWebhookEndpointUpserted,
                    AdminAuditTargetTypes.PartnerWebhookEndpoint,
                    endpoint.Id,
                    OldValue: oldEndpointValue,
                    NewValue: new
                    {
                        endpoint.ApiClientId,
                        Url = GetAuditUrl(endpoint.Url),
                        endpoint.IsActive
                    }),
                cancellationToken);
            await _webhookEndpointRepository.SaveChangesAsync(cancellationToken);
            return Result<PartnerWebhookEndpointResponse>.Success(PartnerIntegrationDashboardMapper.MapEndpoint(endpoint));
        }
        catch (DomainException exception)
        {
            var validationError = ApplicationErrors.ValidationFailed(exception.Message);
            await _credentialAuditWriter.SaveAsync(
                command.CurrentUserId,
                apiClientResult.Value.ShopId,
                apiClientResult.Value.Id,
                PartnerApiCredentialAuditActions.WebhookEndpointUpserted,
                isSuccess: false,
                validationError,
                cancellationToken);

            return Result<PartnerWebhookEndpointResponse>.Failure(validationError);
        }
    }

    public async Task<Result<PartnerWebhookTestResponse>> TestWebhookAsync(
        TestPartnerWebhookCommand command,
        CancellationToken cancellationToken = default)
    {
        var apiClientResult = await _scopeService.GetManageableApiClientAsync(
            command.CurrentUserId,
            command.ApiClientId,
            cancellationToken);
        if (apiClientResult.IsFailure)
        {
            return Result<PartnerWebhookTestResponse>.Failure(apiClientResult.Error);
        }

        if (!apiClientResult.Value.HasScope(PartnerApiScope.WebhookManage))
        {
            return Result<PartnerWebhookTestResponse>.Failure(PartnerApiErrors.MissingScope);
        }

        var endpoint = await _webhookEndpointRepository.GetLatestByApiClientIdAsync(
            apiClientResult.Value.Id,
            cancellationToken);
        if (endpoint is null || !endpoint.IsActive)
        {
            var validationError = ApplicationErrors.ValidationFailed("Active webhook endpoint is required before sending a test event.");
            await _credentialAuditWriter.SaveAsync(
                command.CurrentUserId,
                apiClientResult.Value.ShopId,
                apiClientResult.Value.Id,
                PartnerApiCredentialAuditActions.WebhookTestQueued,
                isSuccess: false,
                validationError,
                cancellationToken);

            return Result<PartnerWebhookTestResponse>.Failure(validationError);
        }

        var eventId = Guid.NewGuid();
        var now = _timeProvider.GetUtcNow();
        var payload = new WebhookTestPayload(
            eventId,
            WebhookEventTypes.WebhookTest,
            "MiniLogistics webhook test event.",
            now);
        var delivery = new WebhookDelivery(
            eventId,
            endpoint.Id,
            apiClientResult.Value.Id,
            WebhookEventTypes.WebhookTest,
            endpoint.Id,
            JsonSerializer.Serialize(payload, PayloadJsonOptions),
            now,
            protectedSigningSecret: endpoint.ProtectedSigningSecret,
            secretVersion: endpoint.SecretVersion);

        await _webhookDeliveryRepository.AddAsync(delivery, cancellationToken);
        await _credentialAuditWriter.AddAsync(
            command.CurrentUserId,
            apiClientResult.Value.ShopId,
            apiClientResult.Value.Id,
            PartnerApiCredentialAuditActions.WebhookTestQueued,
            isSuccess: true,
            error: null,
            cancellationToken);
        await _adminAuditService.RecordAsync(
            new AdminAuditEntry(
                command.CurrentUserId,
                AdminAuditActions.PartnerWebhookTestQueued,
                AdminAuditTargetTypes.WebhookDelivery,
                delivery.Id,
                NewValue: new
                {
                    delivery.ApiClientId,
                    delivery.EventType,
                    delivery.Status
                }),
            cancellationToken);
        await _webhookDeliveryRepository.SaveChangesAsync(cancellationToken);

        return Result<PartnerWebhookTestResponse>.Success(new PartnerWebhookTestResponse(
            delivery.Id,
            delivery.EventType));
    }

    public async Task<Result> RetryWebhookDeliveryAsync(
        RetryPartnerWebhookDeliveryCommand command,
        CancellationToken cancellationToken = default)
    {
        var delivery = await _webhookDeliveryRepository.GetByIdAsync(
            command.WebhookDeliveryId,
            cancellationToken);
        if (delivery is null)
        {
            return Result.Failure(ApplicationErrors.NotFound("Webhook delivery was not found."));
        }

        var apiClientResult = await _scopeService.GetManageableApiClientAsync(
            command.CurrentUserId,
            delivery.ApiClientId,
            cancellationToken);
        if (apiClientResult.IsFailure)
        {
            return Result.Failure(apiClientResult.Error);
        }

        if (!apiClientResult.Value.HasScope(PartnerApiScope.WebhookManage))
        {
            return Result.Failure(PartnerApiErrors.MissingScope);
        }

        var retryResult = delivery.Retry(_timeProvider.GetUtcNow());
        if (retryResult.IsFailure)
        {
            return retryResult;
        }

        await _credentialAuditWriter.AddAsync(
            command.CurrentUserId,
            apiClientResult.Value.ShopId,
            apiClientResult.Value.Id,
            PartnerApiCredentialAuditActions.WebhookDeliveryRetried,
            isSuccess: true,
            error: null,
            cancellationToken);
        await _adminAuditService.RecordAsync(
            new AdminAuditEntry(
                command.CurrentUserId,
                AdminAuditActions.PartnerWebhookDeliveryRetried,
                AdminAuditTargetTypes.WebhookDelivery,
                delivery.Id,
                NewValue: new
                {
                    delivery.ApiClientId,
                    delivery.Status,
                    delivery.NextAttemptAtUtc
                }),
            cancellationToken);
        await _webhookDeliveryRepository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> RetryOutboxMessageAsync(
        RetryPartnerOutboxMessageCommand command,
        CancellationToken cancellationToken = default)
    {
        if (_outboxMessageRepository is null)
        {
            return Result.Failure(ApplicationErrors.ValidationFailed(
                "Outbox recovery is not configured."));
        }

        var message = await _outboxMessageRepository.GetByIdAsync(
            command.OutboxMessageId,
            cancellationToken);
        if (message is null)
        {
            return Result.Failure(ApplicationErrors.NotFound("Outbox message was not found."));
        }

        WebhookDeliveryOutboxPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<WebhookDeliveryOutboxPayload>(
                message.PayloadJson,
                PayloadJsonOptions);
        }
        catch (JsonException)
        {
            payload = null;
        }

        if (payload is null)
        {
            return Result.Failure(ApplicationErrors.NotFound("Outbox message was not found."));
        }

        var apiClientResult = await _scopeService.GetManageableApiClientAsync(
            command.CurrentUserId,
            payload.ApiClientId,
            cancellationToken);
        if (apiClientResult.IsFailure)
        {
            return Result.Failure(apiClientResult.Error);
        }

        if (!apiClientResult.Value.HasScope(PartnerApiScope.WebhookManage))
        {
            return Result.Failure(PartnerApiErrors.MissingScope);
        }

        var retryResult = message.Retry(_timeProvider.GetUtcNow());
        if (retryResult.IsFailure)
        {
            return retryResult;
        }

        await _credentialAuditWriter.AddAsync(
            command.CurrentUserId,
            apiClientResult.Value.ShopId,
            apiClientResult.Value.Id,
            PartnerApiCredentialAuditActions.OutboxMessageRetried,
            isSuccess: true,
            error: null,
            cancellationToken);
        await _adminAuditService.RecordAsync(
            new AdminAuditEntry(
                command.CurrentUserId,
                AdminAuditActions.PartnerOutboxMessageRetried,
                AdminAuditTargetTypes.OutboxMessage,
                message.Id,
                NewValue: new
                {
                    payload.ApiClientId,
                    message.Status,
                    message.NextAttemptAtUtc
                }),
            cancellationToken);
        await _outboxMessageRepository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private static string GetAuditUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return "[invalid webhook URL]";
        }

        var sanitized = uri.GetLeftPart(UriPartial.Path);
        return string.IsNullOrEmpty(uri.Query) ? sanitized : sanitized + "?[redacted]";
    }
}
