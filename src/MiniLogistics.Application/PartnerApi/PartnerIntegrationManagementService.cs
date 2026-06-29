using System.Security.Cryptography;
using System.Text.Json;
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
    private static readonly JsonSerializerOptions PayloadJsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IIdentityService _identityService;
    private readonly IShopRepository _shopRepository;
    private readonly IApiClientRepository _apiClientRepository;
    private readonly IWebhookEndpointRepository _webhookEndpointRepository;
    private readonly IWebhookDeliveryRepository _webhookDeliveryRepository;

    public PartnerIntegrationManagementService(
        IIdentityService identityService,
        IShopRepository shopRepository,
        IApiClientRepository apiClientRepository,
        IWebhookEndpointRepository webhookEndpointRepository,
        IWebhookDeliveryRepository webhookDeliveryRepository)
    {
        _identityService = identityService;
        _shopRepository = shopRepository;
        _apiClientRepository = apiClientRepository;
        _webhookEndpointRepository = webhookEndpointRepository;
        _webhookDeliveryRepository = webhookDeliveryRepository;
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

        var shops = accessResult.Value;
        var shopIds = shops.Select(shop => shop.Id).ToArray();
        var apiClients = await _apiClientRepository.GetByShopIdsAsync(shopIds, cancellationToken);
        var apiClientIds = apiClients.Select(apiClient => apiClient.Id).ToArray();
        var endpoints = await _webhookEndpointRepository.GetByApiClientIdsAsync(apiClientIds, cancellationToken);
        var deliveries = await _webhookDeliveryRepository.GetRecentByApiClientIdsAsync(
            apiClientIds,
            RecentDeliveryCountPerClient,
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

        var response = new PartnerIntegrationDashboardResponse(
            shops.Select(shop => new PartnerIntegrationShopResponse(shop.Id, shop.Name, shop.IsActive)).ToList(),
            apiClients.Select(apiClient =>
            {
                latestEndpoints.TryGetValue(apiClient.Id, out var endpoint);
                deliveriesByClient.TryGetValue(apiClient.Id, out var clientDeliveries);

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
                    clientDeliveries ?? []);
            }).ToList());

        return Result<PartnerIntegrationDashboardResponse>.Success(response);
    }

    public async Task<Result<PartnerApiClientSecretResponse>> CreateApiClientAsync(
        CreatePartnerApiClientCommand command,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
        {
            return Result<PartnerApiClientSecretResponse>.Failure(ApplicationErrors.ValidationFailed("API client name is required."));
        }

        var accessResult = await EnsureCanManageShopAsync(command.CurrentUserId, command.ShopId, cancellationToken);
        if (accessResult.IsFailure)
        {
            return Result<PartnerApiClientSecretResponse>.Failure(accessResult.Error);
        }

        var apiKey = GenerateApiKey();
        var apiClient = new ApiClient(
            command.ShopId,
            command.Name,
            ApiKeyHasher.GetPrefix(apiKey),
            ApiKeyHasher.Hash(apiKey));

        await _apiClientRepository.AddAsync(apiClient, cancellationToken);
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
        var apiKey = GenerateApiKey();
        apiClient.RotateKey(ApiKeyHasher.GetPrefix(apiKey), ApiKeyHasher.Hash(apiKey));
        apiClient.Activate();

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

        if (command.IsActive)
        {
            apiClientResult.Value.Activate();
        }
        else
        {
            apiClientResult.Value.Deactivate();
        }

        await _apiClientRepository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result<PartnerWebhookEndpointResponse>> UpsertWebhookEndpointAsync(
        UpsertPartnerWebhookEndpointCommand command,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.SigningSecret))
        {
            return Result<PartnerWebhookEndpointResponse>.Failure(ApplicationErrors.ValidationFailed("Webhook signing secret is required."));
        }

        var apiClientResult = await GetManageableApiClientAsync(
            command.CurrentUserId,
            command.ApiClientId,
            cancellationToken);
        if (apiClientResult.IsFailure)
        {
            return Result<PartnerWebhookEndpointResponse>.Failure(apiClientResult.Error);
        }

        try
        {
            var endpoint = await _webhookEndpointRepository.GetLatestByApiClientIdAsync(
                apiClientResult.Value.Id,
                cancellationToken);
            if (endpoint is null)
            {
                endpoint = new WebhookEndpoint(apiClientResult.Value.Id, command.Url, command.SigningSecret);
                await _webhookEndpointRepository.AddAsync(endpoint, cancellationToken);
            }
            else
            {
                endpoint.Update(command.Url, command.SigningSecret);
                endpoint.Activate();
            }

            await _webhookEndpointRepository.SaveChangesAsync(cancellationToken);
            return Result<PartnerWebhookEndpointResponse>.Success(MapEndpoint(endpoint));
        }
        catch (DomainException exception)
        {
            return Result<PartnerWebhookEndpointResponse>.Failure(ApplicationErrors.ValidationFailed(exception.Message));
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
            return Result<PartnerWebhookTestResponse>.Failure(ApplicationErrors.ValidationFailed("Active webhook endpoint is required before sending a test event."));
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
        await _webhookDeliveryRepository.SaveChangesAsync(cancellationToken);

        return Result<PartnerWebhookTestResponse>.Success(new PartnerWebhookTestResponse(
            delivery.Id,
            delivery.EventType));
    }

    private async Task<Result<IReadOnlyList<Shop>>> GetAccessibleShopsAsync(
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        var adminCheck = await _identityService.CheckUserRoleAsync(
            currentUserId,
            nameof(UserRole.Admin),
            cancellationToken);
        if (adminCheck.Exists && adminCheck.IsActive && adminCheck.IsInRole)
        {
            return Result<IReadOnlyList<Shop>>.Success(await _shopRepository.GetAllAsync(cancellationToken));
        }

        var shopCheck = await _identityService.CheckUserRoleAsync(
            currentUserId,
            nameof(UserRole.Shop),
            cancellationToken);
        if (!shopCheck.Exists)
        {
            return Result<IReadOnlyList<Shop>>.Failure(ApplicationErrors.NotFound("Current user was not found."));
        }

        if (!shopCheck.IsActive)
        {
            return Result<IReadOnlyList<Shop>>.Failure(ApplicationErrors.Forbidden("Current user is not active."));
        }

        if (!shopCheck.IsInRole)
        {
            return Result<IReadOnlyList<Shop>>.Failure(ApplicationErrors.Forbidden("Only Shop or Admin can manage partner integrations."));
        }

        var shop = await _shopRepository.GetByOwnerUserIdAsync(currentUserId, cancellationToken);
        if (shop is null)
        {
            return Result<IReadOnlyList<Shop>>.Failure(ApplicationErrors.NotFound("Shop was not found for current user."));
        }

        if (!shop.IsActive)
        {
            return Result<IReadOnlyList<Shop>>.Failure(ApplicationErrors.Forbidden("Shop account is not active."));
        }

        return Result<IReadOnlyList<Shop>>.Success([shop]);
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

        return accessibleShopsResult.Value.Any(shop => shop.Id == shopId)
            ? Result.Success()
            : Result.Failure(ApplicationErrors.Forbidden("Current user cannot manage this shop."));
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
            delivery.LastError,
            delivery.CreatedAtUtc);
    }

    private static string GenerateApiKey()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return "ml_live_" + Convert.ToBase64String(bytes)
            .Replace("+", "-", StringComparison.Ordinal)
            .Replace("/", "_", StringComparison.Ordinal)
            .TrimEnd('=');
    }
}
