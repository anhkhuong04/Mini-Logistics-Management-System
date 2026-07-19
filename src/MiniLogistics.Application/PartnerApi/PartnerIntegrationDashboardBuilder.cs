using MiniLogistics.Domain.Common;
using static MiniLogistics.Application.PartnerApi.PartnerIntegrationDashboardMapper;

namespace MiniLogistics.Application.PartnerApi;

public sealed class PartnerIntegrationDashboardBuilder
{
    private const int RecentDeliveryCountPerClient = 10;
    private const int RecentCredentialAuditCountPerClient = 10;

    private readonly IIntegrationScopeService _scopeService;
    private readonly IApiClientRepository _apiClientRepository;
    private readonly IWebhookEndpointRepository _webhookEndpointRepository;
    private readonly IWebhookDeliveryRepository _webhookDeliveryRepository;
    private readonly IPartnerApiCredentialAuditRepository _credentialAuditRepository;

    public PartnerIntegrationDashboardBuilder(
        IIntegrationScopeService scopeService,
        IApiClientRepository apiClientRepository,
        IWebhookEndpointRepository webhookEndpointRepository,
        IWebhookDeliveryRepository webhookDeliveryRepository,
        IPartnerApiCredentialAuditRepository credentialAuditRepository)
    {
        _scopeService = scopeService;
        _apiClientRepository = apiClientRepository;
        _webhookEndpointRepository = webhookEndpointRepository;
        _webhookDeliveryRepository = webhookDeliveryRepository;
        _credentialAuditRepository = credentialAuditRepository;
    }

    public async Task<Result<PartnerIntegrationDashboardResponse>> GetAsync(
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        var accessResult = await _scopeService.GetAccessibleShopsAsync(currentUserId, cancellationToken);
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
}
