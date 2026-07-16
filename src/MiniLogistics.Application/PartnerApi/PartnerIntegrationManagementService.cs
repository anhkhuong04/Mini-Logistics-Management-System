using System.Security.Cryptography;
using System.Text.Json;
using MiniLogistics.Application.AdminAuditing;
using MiniLogistics.Application.Common;
using MiniLogistics.Application.Identity;
using MiniLogistics.Application.Shops;
using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.PartnerApi;
using MiniLogistics.Domain.Shops;
using MiniLogistics.Domain.Users;

namespace MiniLogistics.Application.PartnerApi;

public sealed class PartnerIntegrationManagementService : IPartnerIntegrationManagementService
{
    private const int RecentDeliveryCountPerClient = 10;
    private const int RecentCredentialAuditCountPerClient = 10;
    private static readonly JsonSerializerOptions PayloadJsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IIdentityService _identityService;
    private readonly IShopRepository _shopRepository;
    private readonly IApiClientRepository _apiClientRepository;
    private readonly IWebhookEndpointRepository _webhookEndpointRepository;
    private readonly IWebhookDeliveryRepository _webhookDeliveryRepository;
    private readonly IPartnerApiCredentialAuditRepository _credentialAuditRepository;
    private readonly ISecretProtector _secretProtector;
    private readonly IAdminAuditService _adminAuditService;
    private readonly IIntegrationManagementScopeRepository? _integrationScopeRepository;

    public PartnerIntegrationManagementService(
        IIdentityService identityService,
        IShopRepository shopRepository,
        IApiClientRepository apiClientRepository,
        IWebhookEndpointRepository webhookEndpointRepository,
        IWebhookDeliveryRepository webhookDeliveryRepository,
        IPartnerApiCredentialAuditRepository credentialAuditRepository,
        ISecretProtector secretProtector,
        IAdminAuditService? adminAuditService = null,
        IIntegrationManagementScopeRepository? integrationScopeRepository = null)
    {
        _identityService = identityService;
        _shopRepository = shopRepository;
        _apiClientRepository = apiClientRepository;
        _webhookEndpointRepository = webhookEndpointRepository;
        _webhookDeliveryRepository = webhookDeliveryRepository;
        _credentialAuditRepository = credentialAuditRepository;
        _secretProtector = secretProtector;
        _adminAuditService = adminAuditService ?? NullAdminAuditService.Instance;
        _integrationScopeRepository = integrationScopeRepository;
    }

    public async Task<Result<PartnerIntegrationDashboardResponse>> GetDashboardAsync(
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        var accessResult = await GetAccessibleShopsAsync(currentUserId, cancellationToken);
        if (accessResult.IsFailure)
        {
            return Result<PartnerIntegrationDashboardResponse>.Failure(accessResult.Error);
        }

        var shops = accessResult.Value.Shops;
        var shopIds = shops.Select(shop => shop.Id).ToArray();
        var apiClients = await _apiClientRepository.GetByShopIdsAsync(shopIds, cancellationToken);
        var apiClientIds = apiClients.Select(apiClient => apiClient.Id).ToArray();
        var endpoints = await _webhookEndpointRepository.GetByApiClientIdsAsync(apiClientIds, cancellationToken);
        var deliveries = await _webhookDeliveryRepository.GetRecentByApiClientIdsAsync(
            apiClientIds,
            RecentDeliveryCountPerClient,
            cancellationToken);
        var credentialAudits = await _credentialAuditRepository.GetRecentByApiClientIdsAsync(
            apiClientIds,
            RecentCredentialAuditCountPerClient,
            cancellationToken);

        var shopNames = shops.ToDictionary(shop => shop.Id, shop => shop.Name);
        var latestEndpoints = endpoints
            .GroupBy(endpoint => endpoint.ApiClientId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(endpoint => endpoint.IsActive)
                    .ThenByDescending(endpoint => endpoint.UpdatedAtUtc ?? endpoint.CreatedAtUtc)
                    .First());
        var deliveriesByClient = deliveries
            .GroupBy(delivery => delivery.ApiClientId)
            .ToDictionary(group => group.Key, group => group
                .OrderByDescending(delivery => delivery.CreatedAtUtc)
                .Select(MapDelivery)
                .ToList());
        var credentialAuditsByClient = credentialAudits
            .Where(audit => audit.ApiClientId.HasValue)
            .GroupBy(audit => audit.ApiClientId!.Value)
            .ToDictionary(group => group.Key, group => group
                .OrderByDescending(audit => audit.CreatedAtUtc)
                .Select(MapCredentialAudit)
                .ToList());

        var response = new PartnerIntegrationDashboardResponse(
            shops.Select(shop => new PartnerIntegrationShopResponse(shop.Id, shop.Name, shop.IsActive)).ToList(),
            apiClients.Select(apiClient =>
            {
                latestEndpoints.TryGetValue(apiClient.Id, out var endpoint);
                deliveriesByClient.TryGetValue(apiClient.Id, out var clientDeliveries);
                credentialAuditsByClient.TryGetValue(apiClient.Id, out var clientAudits);

                var mappedDeliveries = clientDeliveries ?? [];
                return new PartnerApiClientResponse(
                    apiClient.Id,
                    apiClient.ShopId,
                    shopNames.GetValueOrDefault(apiClient.ShopId, "Unknown shop"),
                    apiClient.Name,
                    apiClient.ApiKeyPrefix,
                    apiClient.IsActive,
                    apiClient.LastUsedAtUtc,
                    apiClient.CreatedAtUtc,
                    endpoint is null ? null : MapEndpoint(endpoint),
                    mappedDeliveries,
                    BuildWebhookMetrics(mappedDeliveries),
                    clientAudits ?? []);
            }).ToList());

        return Result<PartnerIntegrationDashboardResponse>.Success(
            response with { GranularPermissionEnabled = accessResult.Value.GranularPermissionEnabled });
    }

    public async Task<Result<PartnerApiClientSecretResponse>> CreateApiClientAsync(
        CreatePartnerApiClientCommand command,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
        {
            var validationError = ApplicationErrors.ValidationFailed("API client name is required.");
            await SaveCredentialAuditAsync(
                command.CurrentUserId,
                command.ShopId,
                apiClientId: null,
                PartnerApiCredentialAuditActions.ApiClientCreated,
                isSuccess: false,
                validationError,
                cancellationToken);

            return Result<PartnerApiClientSecretResponse>.Failure(validationError);
        }

        var accessResult = await EnsureCanManageShopAsync(command.CurrentUserId, command.ShopId, cancellationToken);
        if (accessResult.IsFailure)
        {
            await SaveCredentialAuditAsync(
                command.CurrentUserId,
                command.ShopId,
                apiClientId: null,
                PartnerApiCredentialAuditActions.ApiClientCreated,
                isSuccess: false,
                accessResult.Error,
                cancellationToken);

            return Result<PartnerApiClientSecretResponse>.Failure(accessResult.Error);
        }

        var apiKey = GenerateApiKey();
        var apiClient = new ApiClient(
            command.ShopId,
            command.Name,
            ApiKeyHasher.GetPrefix(apiKey),
            ApiKeyHasher.Hash(apiKey));

        await _apiClientRepository.AddAsync(apiClient, cancellationToken);
        await AddCredentialAuditAsync(
            command.CurrentUserId,
            apiClient.ShopId,
            apiClient.Id,
            PartnerApiCredentialAuditActions.ApiClientCreated,
            isSuccess: true,
            error: null,
            cancellationToken);
        await _adminAuditService.RecordAsync(
            new AdminAuditEntry(
                command.CurrentUserId,
                AdminAuditActions.PartnerApiClientCreated,
                AdminAuditTargetTypes.PartnerApiClient,
                apiClient.Id,
                NewValue: new
                {
                    apiClient.ShopId,
                    apiClient.Name,
                    apiClient.ApiKeyPrefix,
                    apiClient.IsActive
                }),
            cancellationToken);
        await _apiClientRepository.SaveChangesAsync(cancellationToken);

        return Result<PartnerApiClientSecretResponse>.Success(new PartnerApiClientSecretResponse(
            apiClient.Id,
            apiKey,
            apiClient.ApiKeyPrefix));
    }

    public async Task<Result<PartnerApiClientSecretResponse>> RotateApiClientKeyAsync(
        RotatePartnerApiClientKeyCommand command,
        CancellationToken cancellationToken = default)
    {
        var apiClientResult = await GetManageableApiClientAsync(
            command.CurrentUserId,
            command.ApiClientId,
            cancellationToken);
        if (apiClientResult.IsFailure)
        {
            return Result<PartnerApiClientSecretResponse>.Failure(apiClientResult.Error);
        }

        var apiClient = apiClientResult.Value;
        var oldApiKeyPrefix = apiClient.ApiKeyPrefix;
        var oldIsActive = apiClient.IsActive;
        var apiKey = GenerateApiKey();
        apiClient.RotateKey(ApiKeyHasher.GetPrefix(apiKey), ApiKeyHasher.Hash(apiKey));
        apiClient.Activate();

        await AddCredentialAuditAsync(
            command.CurrentUserId,
            apiClient.ShopId,
            apiClient.Id,
            PartnerApiCredentialAuditActions.ApiClientKeyRotated,
            isSuccess: true,
            error: null,
            cancellationToken);
        await _adminAuditService.RecordAsync(
            new AdminAuditEntry(
                command.CurrentUserId,
                AdminAuditActions.PartnerApiClientKeyRotated,
                AdminAuditTargetTypes.PartnerApiClient,
                apiClient.Id,
                OldValue: new
                {
                    ApiKeyPrefix = oldApiKeyPrefix,
                    IsActive = oldIsActive
                },
                NewValue: new
                {
                    apiClient.ApiKeyPrefix,
                    apiClient.IsActive
                }),
            cancellationToken);
        await _apiClientRepository.SaveChangesAsync(cancellationToken);

        return Result<PartnerApiClientSecretResponse>.Success(new PartnerApiClientSecretResponse(
            apiClient.Id,
            apiKey,
            apiClient.ApiKeyPrefix));
    }

    public async Task<Result> SetApiClientActiveStatusAsync(
        SetPartnerApiClientActiveStatusCommand command,
        CancellationToken cancellationToken = default)
    {
        var apiClientResult = await GetManageableApiClientAsync(
            command.CurrentUserId,
            command.ApiClientId,
            cancellationToken);
        if (apiClientResult.IsFailure)
        {
            return Result.Failure(apiClientResult.Error);
        }

        var oldIsActive = apiClientResult.Value.IsActive;
        if (command.IsActive)
        {
            apiClientResult.Value.Activate();
        }
        else
        {
            apiClientResult.Value.Deactivate();
        }

        await AddCredentialAuditAsync(
            command.CurrentUserId,
            apiClientResult.Value.ShopId,
            apiClientResult.Value.Id,
            command.IsActive
                ? PartnerApiCredentialAuditActions.ApiClientActivated
                : PartnerApiCredentialAuditActions.ApiClientDeactivated,
            isSuccess: true,
            error: null,
            cancellationToken);
        await _adminAuditService.RecordAsync(
            new AdminAuditEntry(
                command.CurrentUserId,
                AdminAuditActions.PartnerApiClientActiveStatusChanged,
                AdminAuditTargetTypes.PartnerApiClient,
                apiClientResult.Value.Id,
                OldValue: new
                {
                    IsActive = oldIsActive
                },
                NewValue: new
                {
                    apiClientResult.Value.IsActive
                }),
            cancellationToken);
        await _apiClientRepository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result<PartnerWebhookEndpointResponse>> UpsertWebhookEndpointAsync(
        UpsertPartnerWebhookEndpointCommand command,
        CancellationToken cancellationToken = default)
    {
        var apiClientResult = await GetManageableApiClientAsync(
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
            await SaveCredentialAuditAsync(
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
            if (endpoint is null)
            {
                endpoint = new WebhookEndpoint(apiClientResult.Value.Id, command.Url, protectedSigningSecret);
                await _webhookEndpointRepository.AddAsync(endpoint, cancellationToken);
            }
            else
            {
                endpoint.Update(command.Url, protectedSigningSecret);
                endpoint.Activate();
            }

            await AddCredentialAuditAsync(
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
            return Result<PartnerWebhookEndpointResponse>.Success(MapEndpoint(endpoint));
        }
        catch (DomainException exception)
        {
            var validationError = ApplicationErrors.ValidationFailed(exception.Message);
            await SaveCredentialAuditAsync(
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
        var apiClientResult = await GetManageableApiClientAsync(
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
            await SaveCredentialAuditAsync(
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
        var payload = new WebhookTestPayload(
            eventId,
            WebhookEventTypes.WebhookTest,
            "MiniLogistics webhook test event.",
            DateTimeOffset.UtcNow);
        var delivery = new WebhookDelivery(
            eventId,
            endpoint.Id,
            apiClientResult.Value.Id,
            WebhookEventTypes.WebhookTest,
            endpoint.Id,
            JsonSerializer.Serialize(payload, PayloadJsonOptions));

        await _webhookDeliveryRepository.AddAsync(delivery, cancellationToken);
        await AddCredentialAuditAsync(
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

    private async Task<Result<IntegrationShopAccessResult>> GetAccessibleShopsAsync(
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        var adminCheck = await _identityService.CheckUserRoleAsync(
            currentUserId,
            nameof(UserRole.Admin),
            cancellationToken);
        if (adminCheck.Exists && adminCheck.IsActive && adminCheck.IsInRole)
        {
            return await GetAdminAccessibleShopsAsync(currentUserId, cancellationToken);
        }

        var integrationAdminCheck = await _identityService.CheckUserRoleAsync(
            currentUserId,
            nameof(UserRole.IntegrationAdmin),
            cancellationToken);
        if (integrationAdminCheck.Exists && integrationAdminCheck.IsActive && integrationAdminCheck.IsInRole)
        {
            return await GetAdminAccessibleShopsAsync(currentUserId, cancellationToken);
        }

        var shopCheck = await _identityService.CheckUserRoleAsync(
            currentUserId,
            nameof(UserRole.Shop),
            cancellationToken);
        if (!shopCheck.Exists)
        {
            return Result<IntegrationShopAccessResult>.Failure(ApplicationErrors.NotFound("Current user was not found."));
        }

        if (!shopCheck.IsActive)
        {
            return Result<IntegrationShopAccessResult>.Failure(ApplicationErrors.Forbidden("Current user is not active."));
        }

        if (!shopCheck.IsInRole)
        {
            return Result<IntegrationShopAccessResult>.Failure(ApplicationErrors.Forbidden("Only Shop, Admin, or IntegrationAdmin can manage partner integrations."));
        }

        var shops = await _shopRepository.GetAllByOwnerUserIdAsync(currentUserId, cancellationToken);
        if (shops.Count == 0)
        {
            return Result<IntegrationShopAccessResult>.Failure(ApplicationErrors.NotFound("Shop was not found for current user."));
        }

        return Result<IntegrationShopAccessResult>.Success(new IntegrationShopAccessResult(shops, false));
    }

    private async Task<Result<IntegrationShopAccessResult>> GetAdminAccessibleShopsAsync(
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        var shops = await _shopRepository.GetAllAsync(cancellationToken);
        if (_integrationScopeRepository is null)
        {
            return Result<IntegrationShopAccessResult>.Success(new IntegrationShopAccessResult(shops, false));
        }

        var granularPermissionEnabled = await _integrationScopeRepository.AnyActiveScopeAsync(cancellationToken);
        if (!granularPermissionEnabled)
        {
            return Result<IntegrationShopAccessResult>.Success(new IntegrationShopAccessResult(shops, false));
        }

        var scopes = await _integrationScopeRepository.GetActiveByActorUserIdAsync(
            currentUserId,
            cancellationToken);
        if (scopes.Any(scope => scope.IsGlobal))
        {
            return Result<IntegrationShopAccessResult>.Success(new IntegrationShopAccessResult(shops, true));
        }

        var filteredShops = shops
            .Where(shop => scopes.Any(scope => scope.Matches(shop.Id, shop.Address.Province)))
            .ToList();

        return Result<IntegrationShopAccessResult>.Success(new IntegrationShopAccessResult(filteredShops, true));
    }

    private async Task<Result> EnsureCanManageShopAsync(
        Guid currentUserId,
        Guid shopId,
        CancellationToken cancellationToken)
    {
        var accessibleShopsResult = await GetAccessibleShopsAsync(currentUserId, cancellationToken);
        if (accessibleShopsResult.IsFailure)
        {
            return Result.Failure(accessibleShopsResult.Error);
        }

        var shop = accessibleShopsResult.Value.Shops.FirstOrDefault(shop => shop.Id == shopId);
        if (shop is null)
        {
            return Result.Failure(ApplicationErrors.Forbidden("Current user cannot manage this shop."));
        }

        return shop.IsActive
            ? Result.Success()
            : Result.Failure(ApplicationErrors.Forbidden("Shop account is not active."));
    }

    private async Task<Result<ApiClient>> GetManageableApiClientAsync(
        Guid currentUserId,
        Guid apiClientId,
        CancellationToken cancellationToken)
    {
        var apiClient = await _apiClientRepository.GetByIdAsync(apiClientId, cancellationToken);
        if (apiClient is null)
        {
            return Result<ApiClient>.Failure(ApplicationErrors.NotFound("API client was not found."));
        }

        var accessResult = await EnsureCanManageShopAsync(currentUserId, apiClient.ShopId, cancellationToken);
        return accessResult.IsSuccess
            ? Result<ApiClient>.Success(apiClient)
            : Result<ApiClient>.Failure(accessResult.Error);
    }

    private static PartnerWebhookEndpointResponse MapEndpoint(WebhookEndpoint endpoint)
    {
        return new PartnerWebhookEndpointResponse(
            endpoint.Id,
            endpoint.Url,
            endpoint.IsActive,
            endpoint.CreatedAtUtc,
            endpoint.UpdatedAtUtc);
    }

    private static PartnerWebhookDeliveryResponse MapDelivery(WebhookDelivery delivery)
    {
        return new PartnerWebhookDeliveryResponse(
            delivery.Id,
            delivery.EventType,
            delivery.Status,
            delivery.RetryCount,
            delivery.NextAttemptAtUtc,
            delivery.LastAttemptAtUtc,
            delivery.LastResponseStatusCode,
            delivery.LastDurationMs,
            delivery.LastError,
            delivery.CreatedAtUtc);
    }

    private static PartnerWebhookMetricsResponse BuildWebhookMetrics(
        IReadOnlyList<PartnerWebhookDeliveryResponse> deliveries)
    {
        var total = deliveries.Count;
        var succeeded = deliveries.Count(delivery => delivery.Status == WebhookDeliveryStatus.Succeeded);
        var failed = deliveries.Count(delivery => delivery.Status == WebhookDeliveryStatus.Failed);
        var pendingRetry = deliveries.Count(delivery =>
            delivery.Status != WebhookDeliveryStatus.Succeeded
            && delivery.NextAttemptAtUtc.HasValue);
        var successRate = total == 0
            ? 0
            : decimal.Round((decimal)succeeded / total * 100, 2);
        var durations = deliveries
            .Where(delivery => delivery.LastDurationMs.HasValue)
            .Select(delivery => delivery.LastDurationMs!.Value)
            .ToList();
        var averageLatencyMs = durations.Count == 0
            ? (decimal?)null
            : decimal.Round((decimal)durations.Average(), 2);

        return new PartnerWebhookMetricsResponse(
            total,
            succeeded,
            failed,
            pendingRetry,
            successRate,
            averageLatencyMs);
    }

    private static PartnerApiCredentialAuditResponse MapCredentialAudit(PartnerApiCredentialAudit audit)
    {
        return new PartnerApiCredentialAuditResponse(
            audit.Id,
            audit.ActorUserId,
            audit.Action,
            audit.IsSuccess,
            audit.ErrorCode,
            audit.CreatedAtUtc);
    }

    private async Task SaveCredentialAuditAsync(
        Guid actorUserId,
        Guid shopId,
        Guid? apiClientId,
        string action,
        bool isSuccess,
        Error? error,
        CancellationToken cancellationToken)
    {
        await AddCredentialAuditAsync(
            actorUserId,
            shopId,
            apiClientId,
            action,
            isSuccess,
            error,
            cancellationToken);
        await _credentialAuditRepository.SaveChangesAsync(cancellationToken);
    }

    private async Task AddCredentialAuditAsync(
        Guid actorUserId,
        Guid shopId,
        Guid? apiClientId,
        string action,
        bool isSuccess,
        Error? error,
        CancellationToken cancellationToken)
    {
        if (actorUserId == Guid.Empty || shopId == Guid.Empty)
        {
            return;
        }

        var audit = new PartnerApiCredentialAudit(
            actorUserId,
            shopId,
            apiClientId,
            action,
            isSuccess,
            errorCode: error?.Code,
            errorMessage: error?.Description);

        await _credentialAuditRepository.AddAsync(audit, cancellationToken);
    }

    private static string GenerateApiKey()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return "ml_live_" + Convert.ToBase64String(bytes)
            .Replace("+", "-", StringComparison.Ordinal)
            .Replace("/", "_", StringComparison.Ordinal)
            .TrimEnd('=');
    }

    private sealed record IntegrationShopAccessResult(
        IReadOnlyList<Shop> Shops,
        bool GranularPermissionEnabled);
}
