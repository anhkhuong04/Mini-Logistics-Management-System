using System.Text.Json;
using MiniLogistics.Application.AdminAuditing;
using MiniLogistics.Application.Common;
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
    private readonly IAdminAuditService _adminAuditService;
    private readonly TimeProvider _timeProvider;

    public WebhookManagementService(
        IIntegrationScopeService scopeService,
        PartnerIntegrationDashboardBuilder dashboardBuilder,
        IWebhookEndpointRepository webhookEndpointRepository,
        IWebhookDeliveryRepository webhookDeliveryRepository,
        PartnerCredentialAuditWriter credentialAuditWriter,
        ISecretProtector secretProtector,
        TimeProvider timeProvider,
        IAdminAuditService? adminAuditService = null)
    {
        _scopeService = scopeService;
        _dashboardBuilder = dashboardBuilder;
        _webhookEndpointRepository = webhookEndpointRepository;
        _webhookDeliveryRepository = webhookDeliveryRepository;
        _credentialAuditWriter = credentialAuditWriter;
        _secretProtector = secretProtector;
        _timeProvider = timeProvider;
        _adminAuditService = adminAuditService ?? NullAdminAuditService.Instance;
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

        if (string.IsNullOrWhiteSpace(command.SigningSecret))
        {
            var validationError = ApplicationErrors.ValidationFailed("Webhook signing secret is required.");
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
                    endpoint.Url,
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
                        endpoint.Url,
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
            now);

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

}
