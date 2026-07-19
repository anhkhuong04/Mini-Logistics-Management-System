using MiniLogistics.Application.Common;
using MiniLogistics.Application.Identity;
using MiniLogistics.Application.Shops;
using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.PartnerApi;
using MiniLogistics.Domain.Users;

namespace MiniLogistics.Application.PartnerApi;

public sealed class IntegrationScopeService : IIntegrationScopeService
{
    private readonly IIdentityService _identityService;
    private readonly IShopRepository _shopRepository;
    private readonly IApiClientRepository _apiClientRepository;
    private readonly IIntegrationManagementScopeRepository? _integrationScopeRepository;

    public IntegrationScopeService(
        IIdentityService identityService,
        IShopRepository shopRepository,
        IApiClientRepository apiClientRepository,
        IIntegrationManagementScopeRepository? integrationScopeRepository = null)
    {
        _identityService = identityService;
        _shopRepository = shopRepository;
        _apiClientRepository = apiClientRepository;
        _integrationScopeRepository = integrationScopeRepository;
    }

    public async Task<Result<IntegrationShopAccessResult>> GetAccessibleShopsAsync(
        Guid currentUserId,
        CancellationToken cancellationToken = default)
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

    public async Task<Result> EnsureCanManageShopAsync(
        Guid currentUserId,
        Guid shopId,
        CancellationToken cancellationToken = default)
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

    public async Task<Result<ApiClient>> GetManageableApiClientAsync(
        Guid currentUserId,
        Guid apiClientId,
        CancellationToken cancellationToken = default)
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
}
